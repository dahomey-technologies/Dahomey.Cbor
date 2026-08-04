using System;
using System.Buffers;
using System.Buffers.Binary;

namespace Dahomey.Cbor.Serialization
{
    /// <summary>
    /// Outcome of scanning a buffer for one CBOR data item.
    /// </summary>
    public enum CborDataItemStatus
    {
        /// <summary>A complete data item is present.</summary>
        Complete,

        /// <summary>
        /// The buffer ends part-way through a data item. Not an error: read more bytes and retry.
        /// </summary>
        Incomplete,

        /// <summary>
        /// The bytes cannot start a valid data item — a reserved additional-information value, an
        /// indefinite length on a major type that does not allow one, a stray break, a two-byte
        /// simple value below 32, or an indefinite-length string whose chunks are not
        /// definite-length strings of the same type. More data will not help.
        /// </summary>
        Malformed,

        /// <summary>
        /// Nesting exceeded the permitted depth. Deliberately distinct from
        /// <see cref="Malformed"/>: the item may be well-formed but is refused as a resource guard.
        /// </summary>
        TooDeep,

        /// <summary>
        /// The item declares more bytes than <see cref="CborScanLimits.MaxItemSize"/> allows.
        /// Like <see cref="TooDeep"/> this is a resource guard rather than a verdict on the bytes,
        /// and unlike <see cref="Incomplete"/> it tells a streaming caller to stop buffering: the
        /// item will not become acceptable however much more data arrives.
        /// </summary>
        TooLarge,
    }

    /// <summary>
    /// Resource bounds applied while scanning. Both exist because a handful of bytes can describe an
    /// arbitrarily large or arbitrarily deep item, so an unbounded scanner hands untrusted input a
    /// denial-of-service lever.
    /// </summary>
    /// <remarks>
    /// <c>default(CborScanLimits)</c> is <see cref="Default"/> rather than a pair of zeroes, so a
    /// caller cannot accidentally ask for no nesting and no bytes.
    /// </remarks>
    public readonly struct CborScanLimits
    {
        /// <summary>
        /// Default nesting limit. Matches <c>System.Formats.Cbor</c>'s conservative default and is far
        /// above anything real data uses.
        /// </summary>
        public const int DefaultMaxDepth = 64;

        /// <summary>Value of <see cref="MaxItemSize"/> that imposes no size limit.</summary>
        public const long UnlimitedItemSize = long.MaxValue;

        private readonly int _maxDepth;
        private readonly long _maxItemSize;

        /// <summary>Maximum nesting depth. A top-level scalar is depth 1.</summary>
        public int MaxDepth => _maxDepth == 0 ? DefaultMaxDepth : _maxDepth;

        /// <summary>
        /// Maximum length in bytes of a single data item, including everything nested inside it.
        /// </summary>
        public long MaxItemSize => _maxItemSize == 0 ? UnlimitedItemSize : _maxItemSize;

        /// <summary>The limits used by the overloads that do not take any.</summary>
        public static CborScanLimits Default => default;

        /// <param name="maxDepth">Maximum nesting depth. Must be positive.</param>
        /// <param name="maxItemSize">
        /// Maximum length in bytes of a single data item. Must be positive; defaults to no limit.
        /// <para>
        /// Worth setting when scanning an untrusted stream. A declared length is believed long before
        /// its bytes arrive — <c>5B FF FF FF FF FF FF FF FF</c> is nine bytes announcing a 16-exabyte
        /// byte string — and without a cap the scanner can only answer
        /// <see cref="CborDataItemStatus.Incomplete"/>, leaving the caller to buffer forever for bytes
        /// that will never come. With a cap the same input is
        /// <see cref="CborDataItemStatus.TooLarge"/> on the first nine bytes, before anything is
        /// allocated. The bound is checked against declared lengths and item counts, so it holds even
        /// for an item whose payload has not been received.
        /// </para>
        /// </param>
        public CborScanLimits(int maxDepth = DefaultMaxDepth, long maxItemSize = UnlimitedItemSize)
        {
            if (maxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "maxDepth must be positive.");
            }

            if (maxItemSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItemSize), maxItemSize, "maxItemSize must be positive.");
            }

            _maxDepth = maxDepth;
            _maxItemSize = maxItemSize;
        }
    }

    /// <summary>
    /// Determines the byte length of a CBOR data item without decoding it, and without throwing on
    /// truncated input.
    /// </summary>
    /// <remarks>
    /// This is the primitive needed to read CBOR sequences (RFC 8742) and to consume CBOR from a
    /// stream, where a buffer routinely ends in the middle of an item. Asking a decoder to parse and
    /// catching its "unexpected end of buffer" exception works but makes truncation — an ordinary,
    /// expected condition — cost an exception; scanning first is both cheaper and clearer.
    ///
    /// <para>
    /// Both a <see cref="ReadOnlySpan{T}"/> and a <see cref="ReadOnlySequence{T}"/> can be scanned.
    /// The sequence overloads are the ones a streaming caller wants: <c>PipeReader.ReadAsync</c>
    /// hands back a routinely multi-segment <see cref="ReadOnlySequence{T}"/>, and this scanner walks
    /// it across segment boundaries rather than requiring it to be flattened into a contiguous buffer
    /// first. The span overloads remain the fast path for data that is already contiguous, and a
    /// single-segment sequence is dispatched to them.
    /// </para>
    /// <para>
    /// Scanning is structural only: it walks headers and skips payloads. It does not validate UTF-8,
    /// tag semantics, or map key uniqueness, and it accepts non-preferred integer encodings.
    /// <see cref="CborDataItemStatus.Complete"/> therefore means "a decoder will find a whole item
    /// here", not "this item is canonical".
    /// </para>
    /// <para>
    /// Nesting depth and item size are bounded (see <see cref="CborScanLimits"/>) so that hostile
    /// input cannot exhaust the stack or the heap — a buffer of <c>9F</c> bytes describes unbounded
    /// nesting in as many bytes as it has.
    /// </para>
    /// </remarks>
    public static class CborDataItemScanner
    {
        /// <inheritdoc cref="CborScanLimits.DefaultMaxDepth"/>
        public const int DefaultMaxDepth = CborScanLimits.DefaultMaxDepth;

        private const byte BreakByte = 0xFF;
        private const int IndefiniteLength = 31;

        // ---- span ----

        /// <summary>
        /// Determines the length of the data item at the start of <paramref name="buffer"/>.
        /// </summary>
        /// <param name="buffer">Bytes to scan. Only the leading data item is considered.</param>
        /// <param name="length">
        /// The item's length in bytes when the result is <see cref="CborDataItemStatus.Complete"/>;
        /// otherwise the number of bytes consumed before scanning stopped, which is not meaningful.
        /// </param>
        public static CborDataItemStatus Scan(ReadOnlySpan<byte> buffer, out int length)
        {
            return Scan(buffer, CborScanLimits.Default, out length);
        }

        /// <summary>
        /// Determines the length of the data item at the start of <paramref name="buffer"/>, with
        /// explicit resource bounds.
        /// </summary>
        public static CborDataItemStatus Scan(
            ReadOnlySpan<byte> buffer, CborScanLimits limits, out int length)
        {
            int position = 0;
            CborDataItemStatus status = ScanItem(buffer, ref position, limits.MaxDepth, limits.MaxItemSize);
            length = position;
            return status;
        }

        /// <summary>
        /// Convenience wrapper over <see cref="Scan(ReadOnlySpan{byte}, out int)"/>.
        /// </summary>
        /// <returns>true only when a complete data item is present.</returns>
        public static bool TryGetDataItemLength(ReadOnlySpan<byte> buffer, out int length)
        {
            return Scan(buffer, CborScanLimits.Default, out length) == CborDataItemStatus.Complete;
        }

        /// <summary>
        /// Reads one complete data item off the front of <paramref name="buffer"/>, advancing it past
        /// what was consumed. Intended for iterating a CBOR sequence (RFC 8742):
        /// <code>
        /// ReadOnlySpan&lt;byte&gt; remaining = received;
        /// while (CborDataItemScanner.TryReadDataItem(ref remaining, out ReadOnlySpan&lt;byte&gt; item))
        /// {
        ///     Handle(Cbor.Deserialize&lt;T&gt;(item, options));
        /// }
        /// // `remaining` now holds the incomplete tail; prepend it to the next read.
        /// </code>
        /// </summary>
        /// <returns>
        /// true when an item was read; false when the buffer is empty, holds only a partial item, or
        /// starts with malformed bytes. <paramref name="buffer"/> is left untouched when false, so the
        /// caller can distinguish "need more data" from "made progress".
        /// </returns>
        public static bool TryReadDataItem(ref ReadOnlySpan<byte> buffer, out ReadOnlySpan<byte> item)
        {
            return TryReadDataItem(ref buffer, CborScanLimits.Default, out item);
        }

        /// <inheritdoc cref="TryReadDataItem(ref ReadOnlySpan{byte}, out ReadOnlySpan{byte})"/>
        public static bool TryReadDataItem(
            ref ReadOnlySpan<byte> buffer, CborScanLimits limits, out ReadOnlySpan<byte> item)
        {
            if (Scan(buffer, limits, out int length) != CborDataItemStatus.Complete)
            {
                item = default;
                return false;
            }

            item = buffer.Slice(0, length);
            buffer = buffer.Slice(length);
            return true;
        }

        /// <summary>
        /// Counts the complete data items in <paramref name="buffer"/> and reports how many bytes they
        /// occupy, so a caller can tell a cleanly-terminated sequence from a truncated one.
        /// </summary>
        /// <param name="consumed">Bytes occupied by the complete items.</param>
        /// <param name="count">Number of complete items found.</param>
        /// <returns>
        /// The status that stopped the scan: <see cref="CborDataItemStatus.Complete"/> when the buffer
        /// ended exactly on an item boundary, <see cref="CborDataItemStatus.Incomplete"/> when a
        /// partial item trails, or <see cref="CborDataItemStatus.Malformed"/> /
        /// <see cref="CborDataItemStatus.TooDeep"/> / <see cref="CborDataItemStatus.TooLarge"/> when
        /// the remainder cannot be scanned.
        /// </returns>
        public static CborDataItemStatus ScanSequence(
            ReadOnlySpan<byte> buffer, out int consumed, out int count)
        {
            return ScanSequence(buffer, CborScanLimits.Default, out consumed, out count);
        }

        /// <inheritdoc cref="ScanSequence(ReadOnlySpan{byte}, out int, out int)"/>
        public static CborDataItemStatus ScanSequence(
            ReadOnlySpan<byte> buffer, CborScanLimits limits, out int consumed, out int count)
        {
            consumed = 0;
            count = 0;

            while (consumed < buffer.Length)
            {
                CborDataItemStatus status = Scan(buffer.Slice(consumed), limits, out int length);

                if (status != CborDataItemStatus.Complete)
                {
                    return status;
                }

                consumed += length;
                count++;
            }

            return CborDataItemStatus.Complete;
        }

        // ---- sequence ----

        /// <summary>
        /// Determines the length of the data item at the start of <paramref name="buffer"/>, walking
        /// segment boundaries rather than requiring a contiguous buffer.
        /// </summary>
        /// <remarks>
        /// This is the shape a streaming caller has: <c>PipeReader.ReadAsync</c> yields a
        /// <see cref="ReadOnlySequence{T}"/> that is frequently multi-segment, and flattening it
        /// before every scan would reintroduce the copy that reading incrementally is meant to avoid.
        /// </remarks>
        public static CborDataItemStatus Scan(in ReadOnlySequence<byte> buffer, out long length)
        {
            return Scan(buffer, CborScanLimits.Default, out length);
        }

        /// <inheritdoc cref="Scan(in ReadOnlySequence{byte}, out long)"/>
        public static CborDataItemStatus Scan(
            in ReadOnlySequence<byte> buffer, CborScanLimits limits, out long length)
        {
            // A contiguous sequence is exactly the span case; take the cheaper path.
            if (buffer.IsSingleSegment)
            {
                CborDataItemStatus spanStatus = Scan(buffer.First.Span, limits, out int spanLength);
                length = spanLength;
                return spanStatus;
            }

            SequenceReader<byte> reader = new SequenceReader<byte>(buffer);
            CborDataItemStatus status = ScanItem(ref reader, limits.MaxDepth, limits.MaxItemSize);
            length = reader.Consumed;
            return status;
        }

        /// <summary>
        /// Convenience wrapper over <see cref="Scan(in ReadOnlySequence{byte}, out long)"/>.
        /// </summary>
        /// <returns>true only when a complete data item is present.</returns>
        public static bool TryGetDataItemLength(in ReadOnlySequence<byte> buffer, out long length)
        {
            return Scan(buffer, CborScanLimits.Default, out length) == CborDataItemStatus.Complete;
        }

        /// <summary>
        /// Reads one complete data item off the front of <paramref name="buffer"/>, advancing it past
        /// what was consumed. The streaming counterpart of
        /// <see cref="TryReadDataItem(ref ReadOnlySpan{byte}, out ReadOnlySpan{byte})"/>:
        /// <code>
        /// ReadResult result = await pipeReader.ReadAsync(token);
        /// ReadOnlySequence&lt;byte&gt; remaining = result.Buffer;
        /// while (CborDataItemScanner.TryReadDataItem(ref remaining, out ReadOnlySequence&lt;byte&gt; item))
        /// {
        ///     Handle(Cbor.Deserialize&lt;T&gt;(item, options));
        /// }
        /// pipeReader.AdvanceTo(remaining.Start, remaining.End);
        /// </code>
        /// Because <paramref name="buffer"/> is left at the start of the incomplete tail, it can be
        /// handed straight back to <c>PipeReader.AdvanceTo</c> as the consumed position.
        /// </summary>
        /// <returns>
        /// true when an item was read; false when the buffer is empty, holds only a partial item, or
        /// starts with bytes that cannot be scanned. <paramref name="buffer"/> is left untouched when
        /// false.
        /// </returns>
        public static bool TryReadDataItem(
            ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> item)
        {
            return TryReadDataItem(ref buffer, CborScanLimits.Default, out item);
        }

        /// <inheritdoc cref="TryReadDataItem(ref ReadOnlySequence{byte}, out ReadOnlySequence{byte})"/>
        public static bool TryReadDataItem(
            ref ReadOnlySequence<byte> buffer, CborScanLimits limits, out ReadOnlySequence<byte> item)
        {
            if (Scan(buffer, limits, out long length) != CborDataItemStatus.Complete)
            {
                item = default;
                return false;
            }

            item = buffer.Slice(0, length);
            buffer = buffer.Slice(length);
            return true;
        }

        /// <inheritdoc cref="ScanSequence(ReadOnlySpan{byte}, out int, out int)"/>
        public static CborDataItemStatus ScanSequence(
            in ReadOnlySequence<byte> buffer, out long consumed, out int count)
        {
            return ScanSequence(buffer, CborScanLimits.Default, out consumed, out count);
        }

        /// <inheritdoc cref="ScanSequence(ReadOnlySpan{byte}, out int, out int)"/>
        public static CborDataItemStatus ScanSequence(
            in ReadOnlySequence<byte> buffer, CborScanLimits limits, out long consumed, out int count)
        {
            consumed = 0;
            count = 0;

            long total = buffer.Length;

            while (consumed < total)
            {
                CborDataItemStatus status = Scan(buffer.Slice(consumed), limits, out long length);

                if (status != CborDataItemStatus.Complete)
                {
                    return status;
                }

                consumed += length;
                count++;
            }

            return CborDataItemStatus.Complete;
        }

        // ---- span implementation ----

        private static CborDataItemStatus ScanItem(
            ReadOnlySpan<byte> buffer, ref int position, int remainingDepth, long maxItemSize)
        {
            if (remainingDepth <= 0)
            {
                return CborDataItemStatus.TooDeep;
            }

            if (position >= maxItemSize)
            {
                return CborDataItemStatus.TooLarge;
            }

            if (position >= buffer.Length)
            {
                return CborDataItemStatus.Incomplete;
            }

            byte initialByte = buffer[position++];
            int majorType = initialByte >> 5;
            int additionalInfo = initialByte & 0x1F;

            if (additionalInfo == IndefiniteLength)
            {
                return ScanIndefinite(buffer, ref position, remainingDepth, maxItemSize, majorType);
            }

            CborDataItemStatus argumentStatus = ReadArgument(buffer, ref position, additionalInfo, out ulong argument);
            if (argumentStatus != CborDataItemStatus.Complete)
            {
                return argumentStatus;
            }

            switch (majorType)
            {
                case 0: // unsigned integer
                case 1: // negative integer
                    return CborDataItemStatus.Complete;

                case 7: // floating point / simple value — the argument is the whole payload
                    return CheckSimpleValue(additionalInfo, argument);

                case 2: // byte string
                case 3: // text string — `argument` bytes of payload follow
                    if (ExceedsMaxItemSize(argument, position, maxItemSize, bytesPerUnit: 1))
                    {
                        return CborDataItemStatus.TooLarge;
                    }

                    if (argument > (ulong)(buffer.Length - position))
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    position += (int)argument;
                    return CborDataItemStatus.Complete;

                case 4: // array — `argument` items follow
                    // Every item costs at least one byte, so a count the size limit cannot afford is
                    // refused here rather than after the caller has buffered its way towards it.
                    if (ExceedsMaxItemSize(argument, position, maxItemSize, bytesPerUnit: 1))
                    {
                        return CborDataItemStatus.TooLarge;
                    }

                    for (ulong i = 0; i < argument; i++)
                    {
                        // Bail out early rather than spin on a huge count we can never satisfy.
                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        CborDataItemStatus status = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                    return CborDataItemStatus.Complete;

                case 5: // map — `argument` key/value pairs follow
                    if (ExceedsMaxItemSize(argument, position, maxItemSize, bytesPerUnit: 2))
                    {
                        return CborDataItemStatus.TooLarge;
                    }

                    for (ulong i = 0; i < argument; i++)
                    {
                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        CborDataItemStatus keyStatus = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (keyStatus != CborDataItemStatus.Complete)
                        {
                            return keyStatus;
                        }

                        CborDataItemStatus valueStatus = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (valueStatus != CborDataItemStatus.Complete)
                        {
                            return valueStatus;
                        }
                    }

                    return CborDataItemStatus.Complete;

                case 6: // semantic tag — exactly one tagged item follows
                    return ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);

                default:
                    return CborDataItemStatus.Malformed;
            }
        }

        private static CborDataItemStatus ScanIndefinite(
            ReadOnlySpan<byte> buffer, ref int position, int remainingDepth, long maxItemSize, int majorType)
        {
            switch (majorType)
            {
                case 2: // indefinite-length byte string
                case 3: // indefinite-length text string
                    // RFC 8949: chunks must be definite-length strings of the same major type.
                    while (true)
                    {
                        if (position >= maxItemSize)
                        {
                            return CborDataItemStatus.TooLarge;
                        }

                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (buffer[position] == BreakByte)
                        {
                            position++;
                            return CborDataItemStatus.Complete;
                        }

                        int chunkMajorType = buffer[position] >> 5;
                        int chunkAdditionalInfo = buffer[position] & 0x1F;

                        if (chunkMajorType != majorType || chunkAdditionalInfo == IndefiniteLength)
                        {
                            return CborDataItemStatus.Malformed;
                        }

                        CborDataItemStatus status = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                case 4: // indefinite-length array
                    while (true)
                    {
                        if (position >= maxItemSize)
                        {
                            return CborDataItemStatus.TooLarge;
                        }

                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (buffer[position] == BreakByte)
                        {
                            position++;
                            return CborDataItemStatus.Complete;
                        }

                        CborDataItemStatus status = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                case 5: // indefinite-length map
                    while (true)
                    {
                        if (position >= maxItemSize)
                        {
                            return CborDataItemStatus.TooLarge;
                        }

                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (buffer[position] == BreakByte)
                        {
                            position++;
                            return CborDataItemStatus.Complete;
                        }

                        CborDataItemStatus keyStatus = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (keyStatus != CborDataItemStatus.Complete)
                        {
                            return keyStatus;
                        }

                        // A map must not end between a key and its value.
                        if (position < buffer.Length && buffer[position] == BreakByte)
                        {
                            return CborDataItemStatus.Malformed;
                        }

                        CborDataItemStatus valueStatus = ScanItem(buffer, ref position, remainingDepth - 1, maxItemSize);
                        if (valueStatus != CborDataItemStatus.Complete)
                        {
                            return valueStatus;
                        }
                    }

                default:
                    // Major types 0, 1, 6 and 7 have no indefinite form. For type 7 additional
                    // info 31 is the break code, which is only valid inside an indefinite-length
                    // item and is therefore malformed where an item was expected.
                    return CborDataItemStatus.Malformed;
            }
        }

        private static CborDataItemStatus ReadArgument(
            ReadOnlySpan<byte> buffer, ref int position, int additionalInfo, out ulong argument)
        {
            argument = 0;

            if (additionalInfo <= 23)
            {
                argument = (ulong)additionalInfo;
                return CborDataItemStatus.Complete;
            }

            switch (additionalInfo)
            {
                case 24:
                    if (position + 1 > buffer.Length)
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    argument = buffer[position];
                    position += 1;
                    return CborDataItemStatus.Complete;

                case 25:
                    if (position + 2 > buffer.Length)
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    argument = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(position));
                    position += 2;
                    return CborDataItemStatus.Complete;

                case 26:
                    if (position + 4 > buffer.Length)
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    argument = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(position));
                    position += 4;
                    return CborDataItemStatus.Complete;

                case 27:
                    if (position + 8 > buffer.Length)
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    argument = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(position));
                    position += 8;
                    return CborDataItemStatus.Complete;

                default:
                    // 28, 29 and 30 are reserved by RFC 8949.
                    return CborDataItemStatus.Malformed;
            }
        }

        // ---- sequence implementation ----
        //
        // A deliberate mirror of the span implementation above rather than a shared one: the two
        // cursors are a `ref int` into a span and a `SequenceReader<byte>`, and a ref struct cannot be
        // a generic type argument on netstandard2.0, so there is no way to write the walk once without
        // pushing the contiguous case through SequenceReader and paying for it there. The tests scan
        // every corpus entry as a span, as a single-segment sequence and as a byte-per-segment
        // sequence, and assert all three agree — that equivalence is what keeps the two in step.

        private static CborDataItemStatus ScanItem(
            ref SequenceReader<byte> reader, int remainingDepth, long maxItemSize)
        {
            if (remainingDepth <= 0)
            {
                return CborDataItemStatus.TooDeep;
            }

            if (reader.Consumed >= maxItemSize)
            {
                return CborDataItemStatus.TooLarge;
            }

            if (!reader.TryRead(out byte initialByte))
            {
                return CborDataItemStatus.Incomplete;
            }

            int majorType = initialByte >> 5;
            int additionalInfo = initialByte & 0x1F;

            if (additionalInfo == IndefiniteLength)
            {
                return ScanIndefinite(ref reader, remainingDepth, maxItemSize, majorType);
            }

            CborDataItemStatus argumentStatus = ReadArgument(ref reader, additionalInfo, out ulong argument);
            if (argumentStatus != CborDataItemStatus.Complete)
            {
                return argumentStatus;
            }

            switch (majorType)
            {
                case 0: // unsigned integer
                case 1: // negative integer
                    return CborDataItemStatus.Complete;

                case 7: // floating point / simple value
                    return CheckSimpleValue(additionalInfo, argument);

                case 2: // byte string
                case 3: // text string
                    if (ExceedsMaxItemSize(argument, reader.Consumed, maxItemSize, bytesPerUnit: 1))
                    {
                        return CborDataItemStatus.TooLarge;
                    }

                    if (argument > (ulong)reader.Remaining)
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    reader.Advance((long)argument);
                    return CborDataItemStatus.Complete;

                case 4: // array
                    if (ExceedsMaxItemSize(argument, reader.Consumed, maxItemSize, bytesPerUnit: 1))
                    {
                        return CborDataItemStatus.TooLarge;
                    }

                    for (ulong i = 0; i < argument; i++)
                    {
                        if (reader.End)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        CborDataItemStatus status = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                    return CborDataItemStatus.Complete;

                case 5: // map
                    if (ExceedsMaxItemSize(argument, reader.Consumed, maxItemSize, bytesPerUnit: 2))
                    {
                        return CborDataItemStatus.TooLarge;
                    }

                    for (ulong i = 0; i < argument; i++)
                    {
                        if (reader.End)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        CborDataItemStatus keyStatus = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (keyStatus != CborDataItemStatus.Complete)
                        {
                            return keyStatus;
                        }

                        CborDataItemStatus valueStatus = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (valueStatus != CborDataItemStatus.Complete)
                        {
                            return valueStatus;
                        }
                    }

                    return CborDataItemStatus.Complete;

                case 6: // semantic tag
                    return ScanItem(ref reader, remainingDepth - 1, maxItemSize);

                default:
                    return CborDataItemStatus.Malformed;
            }
        }

        private static CborDataItemStatus ScanIndefinite(
            ref SequenceReader<byte> reader, int remainingDepth, long maxItemSize, int majorType)
        {
            switch (majorType)
            {
                case 2: // indefinite-length byte string
                case 3: // indefinite-length text string
                    while (true)
                    {
                        if (reader.Consumed >= maxItemSize)
                        {
                            return CborDataItemStatus.TooLarge;
                        }

                        if (!reader.TryPeek(out byte next))
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (next == BreakByte)
                        {
                            reader.Advance(1);
                            return CborDataItemStatus.Complete;
                        }

                        int chunkMajorType = next >> 5;
                        int chunkAdditionalInfo = next & 0x1F;

                        if (chunkMajorType != majorType || chunkAdditionalInfo == IndefiniteLength)
                        {
                            return CborDataItemStatus.Malformed;
                        }

                        CborDataItemStatus status = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                case 4: // indefinite-length array
                    while (true)
                    {
                        if (reader.Consumed >= maxItemSize)
                        {
                            return CborDataItemStatus.TooLarge;
                        }

                        if (!reader.TryPeek(out byte next))
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (next == BreakByte)
                        {
                            reader.Advance(1);
                            return CborDataItemStatus.Complete;
                        }

                        CborDataItemStatus status = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                case 5: // indefinite-length map
                    while (true)
                    {
                        if (reader.Consumed >= maxItemSize)
                        {
                            return CborDataItemStatus.TooLarge;
                        }

                        if (!reader.TryPeek(out byte next))
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (next == BreakByte)
                        {
                            reader.Advance(1);
                            return CborDataItemStatus.Complete;
                        }

                        CborDataItemStatus keyStatus = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (keyStatus != CborDataItemStatus.Complete)
                        {
                            return keyStatus;
                        }

                        // A map must not end between a key and its value.
                        if (reader.TryPeek(out byte afterKey) && afterKey == BreakByte)
                        {
                            return CborDataItemStatus.Malformed;
                        }

                        CborDataItemStatus valueStatus = ScanItem(ref reader, remainingDepth - 1, maxItemSize);
                        if (valueStatus != CborDataItemStatus.Complete)
                        {
                            return valueStatus;
                        }
                    }

                default:
                    return CborDataItemStatus.Malformed;
            }
        }

        private static CborDataItemStatus ReadArgument(
            ref SequenceReader<byte> reader, int additionalInfo, out ulong argument)
        {
            argument = 0;

            if (additionalInfo <= 23)
            {
                argument = (ulong)additionalInfo;
                return CborDataItemStatus.Complete;
            }

            int argumentLength;

            switch (additionalInfo)
            {
                case 24: argumentLength = 1; break;
                case 25: argumentLength = 2; break;
                case 26: argumentLength = 4; break;
                case 27: argumentLength = 8; break;

                default:
                    // 28, 29 and 30 are reserved by RFC 8949.
                    return CborDataItemStatus.Malformed;
            }

            Span<byte> bytes = stackalloc byte[8];
            Span<byte> argumentBytes = bytes.Slice(0, argumentLength);

            if (!reader.TryCopyTo(argumentBytes))
            {
                return CborDataItemStatus.Incomplete;
            }

            reader.Advance(argumentLength);

            switch (argumentLength)
            {
                case 1:
                    argument = argumentBytes[0];
                    break;

                case 2:
                    argument = BinaryPrimitives.ReadUInt16BigEndian(argumentBytes);
                    break;

                case 4:
                    argument = BinaryPrimitives.ReadUInt32BigEndian(argumentBytes);
                    break;

                default:
                    argument = BinaryPrimitives.ReadUInt64BigEndian(argumentBytes);
                    break;
            }

            return CborDataItemStatus.Complete;
        }

        // ---- shared checks ----

        /// <summary>
        /// RFC 8949 §3.3: the two-byte form of major type 7 must not encode a value below 32, because
        /// those simple values have a one-byte form. A decoder rejects such input, so reporting
        /// <see cref="CborDataItemStatus.Complete"/> would break the contract that a complete scan
        /// means a decoder will find a whole item here.
        /// </summary>
        private static CborDataItemStatus CheckSimpleValue(int additionalInfo, ulong argument)
        {
            if (additionalInfo == 24 && argument < 32)
            {
                return CborDataItemStatus.Malformed;
            }

            return CborDataItemStatus.Complete;
        }

        /// <summary>
        /// Decides whether a declared count busts the size limit, given that each unit — a payload
        /// byte, an array item, or a map key/value pair — costs at least
        /// <paramref name="bytesPerUnit"/> bytes. Phrased as a division so that a count near
        /// <see cref="ulong.MaxValue"/> cannot overflow into looking affordable.
        /// </summary>
        private static bool ExceedsMaxItemSize(ulong count, long position, long maxItemSize, int bytesPerUnit)
        {
            if (position >= maxItemSize)
            {
                return true;
            }

            ulong affordable = (ulong)(maxItemSize - position) / (ulong)bytesPerUnit;
            return count > affordable;
        }
    }
}
