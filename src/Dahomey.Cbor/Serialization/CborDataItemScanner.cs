using System;
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
        /// indefinite length on a major type that does not allow one, a stray break, or an
        /// indefinite-length string whose chunks are not definite-length strings of the same type.
        /// More data will not help.
        /// </summary>
        Malformed,

        /// <summary>
        /// Nesting exceeded the permitted depth. Deliberately distinct from
        /// <see cref="Malformed"/>: the item may be well-formed but is refused as a resource guard.
        /// </summary>
        TooDeep,
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
    /// Scanning is structural only: it walks headers and skips payloads. It does not validate string
    /// contents, tag semantics, or map key uniqueness, and it accepts non-preferred integer
    /// encodings. <see cref="CborDataItemStatus.Complete"/> therefore means "a decoder will find a
    /// whole item here", not "this item is canonical".
    /// </para>
    /// <para>
    /// Nesting depth is bounded (<see cref="DefaultMaxDepth"/>) so that hostile input cannot exhaust
    /// the stack — a buffer of <c>9F</c> bytes describes unbounded nesting in as many bytes as it has.
    /// </para>
    /// </remarks>
    public static class CborDataItemScanner
    {
        /// <summary>
        /// Default nesting limit. Matches <c>System.Formats.Cbor</c>'s conservative default and is far
        /// above anything real data uses.
        /// </summary>
        public const int DefaultMaxDepth = 64;

        private const byte BreakByte = 0xFF;
        private const int IndefiniteLength = 31;

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
            return Scan(buffer, DefaultMaxDepth, out length);
        }

        /// <summary>
        /// Determines the length of the data item at the start of <paramref name="buffer"/>, with an
        /// explicit nesting limit.
        /// </summary>
        /// <param name="maxDepth">
        /// Maximum nesting depth. A top-level scalar is depth 1. Must be positive.
        /// </param>
        public static CborDataItemStatus Scan(ReadOnlySpan<byte> buffer, int maxDepth, out int length)
        {
            if (maxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "maxDepth must be positive.");
            }

            int position = 0;
            CborDataItemStatus status = ScanItem(buffer, ref position, maxDepth);
            length = position;
            return status;
        }

        /// <summary>
        /// Convenience wrapper over <see cref="Scan(ReadOnlySpan{byte}, out int)"/>.
        /// </summary>
        /// <returns>true only when a complete data item is present.</returns>
        public static bool TryGetDataItemLength(ReadOnlySpan<byte> buffer, out int length)
        {
            return Scan(buffer, DefaultMaxDepth, out length) == CborDataItemStatus.Complete;
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
            if (Scan(buffer, DefaultMaxDepth, out int length) != CborDataItemStatus.Complete)
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
        /// <see cref="CborDataItemStatus.TooDeep"/> when the remainder cannot be scanned.
        /// </returns>
        public static CborDataItemStatus ScanSequence(
            ReadOnlySpan<byte> buffer, out int consumed, out int count)
        {
            consumed = 0;
            count = 0;

            while (consumed < buffer.Length)
            {
                CborDataItemStatus status = Scan(buffer.Slice(consumed), DefaultMaxDepth, out int length);

                if (status != CborDataItemStatus.Complete)
                {
                    return status;
                }

                consumed += length;
                count++;
            }

            return CborDataItemStatus.Complete;
        }

        private static CborDataItemStatus ScanItem(
            ReadOnlySpan<byte> buffer, ref int position, int remainingDepth)
        {
            if (remainingDepth <= 0)
            {
                return CborDataItemStatus.TooDeep;
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
                return ScanIndefinite(buffer, ref position, remainingDepth, majorType);
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
                case 7: // floating point / simple value — the argument is the whole payload
                    return CborDataItemStatus.Complete;

                case 2: // byte string
                case 3: // text string — `argument` bytes of payload follow
                    if (argument > (ulong)(buffer.Length - position))
                    {
                        return CborDataItemStatus.Incomplete;
                    }

                    position += (int)argument;
                    return CborDataItemStatus.Complete;

                case 4: // array — `argument` items follow
                    for (ulong i = 0; i < argument; i++)
                    {
                        // Bail out early rather than spin on a huge count we can never satisfy.
                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        CborDataItemStatus status = ScanItem(buffer, ref position, remainingDepth - 1);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                    return CborDataItemStatus.Complete;

                case 5: // map — `argument` key/value pairs follow
                    for (ulong i = 0; i < argument; i++)
                    {
                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        CborDataItemStatus keyStatus = ScanItem(buffer, ref position, remainingDepth - 1);
                        if (keyStatus != CborDataItemStatus.Complete)
                        {
                            return keyStatus;
                        }

                        CborDataItemStatus valueStatus = ScanItem(buffer, ref position, remainingDepth - 1);
                        if (valueStatus != CborDataItemStatus.Complete)
                        {
                            return valueStatus;
                        }
                    }

                    return CborDataItemStatus.Complete;

                case 6: // semantic tag — exactly one tagged item follows
                    return ScanItem(buffer, ref position, remainingDepth - 1);

                default:
                    return CborDataItemStatus.Malformed;
            }
        }

        private static CborDataItemStatus ScanIndefinite(
            ReadOnlySpan<byte> buffer, ref int position, int remainingDepth, int majorType)
        {
            switch (majorType)
            {
                case 2: // indefinite-length byte string
                case 3: // indefinite-length text string
                    // RFC 8949: chunks must be definite-length strings of the same major type.
                    while (true)
                    {
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

                        CborDataItemStatus status = ScanItem(buffer, ref position, remainingDepth - 1);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                case 4: // indefinite-length array
                    while (true)
                    {
                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (buffer[position] == BreakByte)
                        {
                            position++;
                            return CborDataItemStatus.Complete;
                        }

                        CborDataItemStatus status = ScanItem(buffer, ref position, remainingDepth - 1);
                        if (status != CborDataItemStatus.Complete)
                        {
                            return status;
                        }
                    }

                case 5: // indefinite-length map
                    while (true)
                    {
                        if (position >= buffer.Length)
                        {
                            return CborDataItemStatus.Incomplete;
                        }

                        if (buffer[position] == BreakByte)
                        {
                            position++;
                            return CborDataItemStatus.Complete;
                        }

                        CborDataItemStatus keyStatus = ScanItem(buffer, ref position, remainingDepth - 1);
                        if (keyStatus != CborDataItemStatus.Complete)
                        {
                            return keyStatus;
                        }

                        // A map must not end between a key and its value.
                        if (position < buffer.Length && buffer[position] == BreakByte)
                        {
                            return CborDataItemStatus.Malformed;
                        }

                        CborDataItemStatus valueStatus = ScanItem(buffer, ref position, remainingDepth - 1);
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
    }
}
