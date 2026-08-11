using Dahomey.Cbor.Util;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Dahomey.Cbor.Serialization
{
    public enum CborDataItemType
    {
        Boolean,
        Null,
        Signed,
        Unsigned,
        Single,
        Double,
        String,
        ByteString,
        Array,
        Map,
        Break,

        Decimal
    }

    public interface ICborMapReader<TC>
    {
        void ReadBeginMap(int size, ref TC context);
        void ReadMapItem(ref CborReader reader, ref TC context);
    }

    public interface ICborArrayReader<TC>
    {
        void ReadBeginArray(int size, ref TC context);
        void ReadArrayItem(ref CborReader reader, ref TC context);
    }

    public enum CborReaderState
    {
        Start,
        Header,
        Data
    }

    [StructLayout(LayoutKind.Explicit, Size = 2)]
    public struct CborReaderHeader
    {
        [FieldOffset(0)]
        public CborMajorType MajorType;

        [FieldOffset(1)]
        public byte AdditionalValue;

        [FieldOffset(1)]
        public CborPrimitive Primitive;
    }

    public ref struct CborReaderBookmark
    {
        public ReadOnlySpan<byte> buffer;
        public ReadOnlySequence<byte>? sequence;
        public int currentPos;
        public int length;
        public SequenceReader<byte> sequenceReader;
        public CborReaderState state;
        public CborReaderHeader header;
        public int remainingItemCount;
    }

    public ref struct CborReader
    {
        private const int CHUNK_SIZE = 1024;
        private const byte INDEFINITE_LENGTH = 31;
        private const int SCRATCH_BUFFER_SIZE = 16; // This is enough for storing decimal bytes

        // RFC 8949 §3.4.3.
        private const ulong UNSIGNED_BIGNUM_TAG = 2;
        private const ulong NEGATIVE_BIGNUM_TAG = 3;

        private ReadOnlySpan<byte> _buffer;
        private ReadOnlySequence<byte>? _sequence;
        private int _currentPos;
        private int _length;
        private SequenceReader<byte> _sequenceReader;
        private CborReaderState _state;
        private CborReaderHeader _header;
        private int _remainingItemCount;
        private byte[]? _scratchBuffer;
        private readonly int _maxDepth;
        private int _depth;

        public bool DataAvailable => _currentPos < _length;

        public ReadOnlySpan<byte> Buffer => _sequence.HasValue
            ? throw new InvalidOperationException("Buffer is not available when reader is operating on a sequence buffer")
            : _buffer.Slice(_currentPos);

        /// <summary>Maximum permitted nesting depth of maps and arrays.</summary>
        public int MaxDepth => _maxDepth;

        public CborReader(ReadOnlySpan<byte> buffer)
            : this(buffer, CborWriter.DefaultMaxDepth)
        {
        }

        /// <param name="buffer">The document to read.</param>
        /// <param name="maxDepth">
        /// Maximum nesting depth of maps and arrays. Exceeding it throws a <see cref="CborException"/>
        /// rather than recursing until the stack is exhausted, which is what deeply nested hostile
        /// input would otherwise cause. Must be positive.
        /// </param>
        public CborReader(ReadOnlySpan<byte> buffer, int maxDepth)
        {
            if (maxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "maxDepth must be positive.");
            }

            _buffer = buffer;
            _sequence = null;
            _currentPos = 0;
            _length = buffer.Length;
            _sequenceReader = default;
            _state = CborReaderState.Start;
            _header = new CborReaderHeader();
            _remainingItemCount = 0;
            _scratchBuffer = null;
            _maxDepth = maxDepth;
            _depth = 0;
        }

        public CborReader(ReadOnlySequence<byte> buffer)
            : this(buffer, CborWriter.DefaultMaxDepth)
        {
        }

        /// <inheritdoc cref="CborReader(ReadOnlySpan{byte}, int)"/>
        public CborReader(ReadOnlySequence<byte> buffer, int maxDepth)
        {
            if (maxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "maxDepth must be positive.");
            }

            _buffer = ReadOnlySpan<byte>.Empty;
            _sequence = buffer;
            _currentPos = 0;
            _length = (int)buffer.Length;
            _sequenceReader = new SequenceReader<byte>(buffer);
            _state = CborReaderState.Start;
            _header = new CborReaderHeader();
            _remainingItemCount = 0;
            _scratchBuffer = null;
            _maxDepth = maxDepth;
            _depth = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadSemanticTag(out ulong semanticTag)
        {
            if (Accept(CborMajorType.SemanticTag))
            {
                semanticTag = ReadInteger();
                _state = CborReaderState.Data;
                return true;
            }

            semanticTag = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CborDataItemType GetCurrentDataItemType()
        {
            SkipSemanticTag();
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return CborDataItemType.Unsigned;

                case CborMajorType.NegativeInteger:
                    return CborDataItemType.Signed;

                case CborMajorType.ByteString:
                    return CborDataItemType.ByteString;

                case CborMajorType.TextString:
                    return CborDataItemType.String;

                case CborMajorType.Array:
                    return CborDataItemType.Array;

                case CborMajorType.Map:
                    return CborDataItemType.Map;

                case CborMajorType.SemanticTag:
                    // Unreachable: SkipSemanticTag above consumes the whole stack. Kept consuming the
                    // tag by reading its argument rather than by byte count, because the byte count is
                    // what this arm used to get wrong - Advance(1) skipped the head of the tagged item
                    // on top of the tag head GetHeader had already taken, and could not have been
                    // right anyway for a multi-byte tag head such as D8 64.
                    ReadInteger();
                    _state = CborReaderState.Data;
                    return GetCurrentDataItemType();

                case CborMajorType.Primitive:
                    switch (header.Primitive)
                    {
                        case CborPrimitive.True:
                        case CborPrimitive.False:
                            return CborDataItemType.Boolean;

                        case CborPrimitive.Null:
                        case CborPrimitive.Undefined:
                            return CborDataItemType.Null;

                        case CborPrimitive.HalfFloat:
                        case CborPrimitive.SingleFloat:
                            return CborDataItemType.Single;

                        case CborPrimitive.DoubleFloat:
                            return CborDataItemType.Double;

                        case CborPrimitive.DecimalFloat:
                            return CborDataItemType.Decimal;

                        case CborPrimitive.Break:
                            return CborDataItemType.Break;

                        default:
                            ThrowCbor("Primitive not supported");
                            return default; // Unreachable
                    }

                default:
                    ThrowCbor("Major type not supported");
                    return default; // Unreachable
            }
        }

        /// <summary>
        /// Reports whether the next data item is the break marker that terminates an
        /// indefinite-length array or map.
        /// </summary>
        /// <remarks>
        /// Deliberately does not skip a semantic tag: a break marker is never tagged, so a tag here
        /// belongs to the next item and has to survive for that item's converter. A typed array is
        /// decoded from its RFC 8746 tag, so consuming it would make an indefinite-length container
        /// of typed arrays unreadable.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsBreak()
        {
            CborReaderHeader header = GetHeader();

            return header.MajorType == CborMajorType.Primitive && header.Primitive == CborPrimitive.Break;
        }

        /// <summary>
        /// Reports whether the next data item is the null primitive, or <c>undefined</c>, which
        /// <see cref="GetCurrentDataItemType"/> also reports as <see cref="CborDataItemType.Null"/>.
        /// </summary>
        /// <remarks>
        /// Like <see cref="IsBreak"/>, this inspects the header and skips nothing:
        /// <see cref="GetCurrentDataItemType"/> begins with <c>SkipSemanticTag()</c>, so a caller that
        /// only wants to look - a member probing for null - would consume a tag the value's own
        /// converter still needs.
        /// <para>
        /// It is deliberately not a bookmark-and-rewind. This runs once per member of every object
        /// read, and <see cref="CborReaderBookmark"/> carries a span, a sequence and a
        /// <c>SequenceReader</c>: copying that twice per member is a cost paid by every caller,
        /// including those who never encounter a semantic tag.
        /// </para>
        /// <para>
        /// One consequence, which is the reason this is not simply equivalent: a null behind a
        /// semantic tag reads as a tag rather than as null, so the probe does not see it. The value's
        /// converter still calls <c>ReadNull()</c>, which skips the tag and yields null, so the value
        /// is unaffected -- but <see cref="Attributes.RequirementPolicy.DisallowNull"/> no longer rejects that
        /// one exotic shape. Pinned by <c>ATaggedNullIsNotRejectedByDisallowNull</c> and
        /// <c>ATaggedUndefinedIsNotRejectedByDisallowNull</c>.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNull()
        {
            CborReaderHeader header = GetHeader();

            return header.MajorType == CborMajorType.Primitive
                && (header.Primitive == CborPrimitive.Null || header.Primitive == CborPrimitive.Undefined);
        }

        /// <summary>
        /// Reports whether the next data item carries a semantic tag.
        /// </summary>
        /// <remarks>
        /// Like <see cref="IsNull"/> and <see cref="IsBreak"/> this inspects the header and skips
        /// nothing. It lets a converter that only sometimes wants the tag pay for a bookmark on the rare
        /// tagged item rather than on every item.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsSemanticTag()
        {
            return GetHeader().MajorType == CborMajorType.SemanticTag;
        }

        public CborReaderBookmark GetBookmark()
        {
            CborReaderBookmark bookmark;

            bookmark.buffer = _buffer;
            bookmark.sequence = _sequence;
            bookmark.currentPos = _currentPos;
            bookmark.length = _length;
            bookmark.sequenceReader = _sequenceReader;
            bookmark.state = _state;
            bookmark.header = _header;
            bookmark.remainingItemCount = _remainingItemCount;

            return bookmark;
        }

        public void ReturnToBookmark(CborReaderBookmark bookmark)
        {
            _buffer = bookmark.buffer;
            _sequence = bookmark.sequence;
            _currentPos = bookmark.currentPos;
            _length = bookmark.length;
            _sequenceReader = bookmark.sequenceReader;
            _state = bookmark.state;
            _header = bookmark.header;
            _remainingItemCount = bookmark.remainingItemCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBeginArray()
        {
            SkipSemanticTag();
            Expect(CborMajorType.Array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadBeginMap()
        {
            SkipSemanticTag();
            Expect(CborMajorType.Map);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadNull()
        {
            SkipSemanticTag();
            return Accept(CborPrimitive.Null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean()
        {
            SkipSemanticTag();
            if (Accept(CborPrimitive.True))
            {
                return true;
            }

            Expect(CborPrimitive.False);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            SkipSemanticTag();
            return ReadUnsigned(ulong.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
        {
            SkipSemanticTag();
            return ReadSigned(long.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            SkipSemanticTag();
            return (uint)ReadUnsigned(uint.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            SkipSemanticTag();
            return (int)ReadSigned(int.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            SkipSemanticTag();
            return (ushort)ReadUnsigned(ushort.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16()
        {
            SkipSemanticTag();
            return (short)ReadSigned(short.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            SkipSemanticTag();
            return (byte)ReadUnsigned(byte.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte()
        {
            SkipSemanticTag();
            return (sbyte)ReadSigned(sbyte.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string? ReadString()
        {
            if (ReadNull())
            {
                return null;
            }

            Expect(CborMajorType.TextString);
            return Encoding.UTF8.GetString(ReadSizeAndBytes(allowScratchBuffer: true));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar()
        {
            ReadOnlySpan<byte> bytes = ReadRawString();
            char result;
            unsafe
            {
                fixed (byte* rawBytes = bytes)
                {
                    try
                    {
                        Encoding.UTF8.GetChars(rawBytes, bytes.Length, &result, 1);
                    }
                    catch (ArgumentException)
                    {
                        throw new CborException("Cannot read single char from buffer");
                    }
                }
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadRawString()
        {
            if (ReadNull())
            {
                return null;
            }

            Expect(CborMajorType.TextString);
            return ReadSizeAndBytes(allowScratchBuffer: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadByteString()
        {
            SkipSemanticTag();
            Expect(CborMajorType.ByteString);
            return ReadSizeAndBytes(allowScratchBuffer: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySequence<byte> ReadByteStringSequence()
        {
            SkipSemanticTag();
            Expect(CborMajorType.ByteString);
            return ReadSizeAndSequence();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Half ReadHalf()
        {
            SkipSemanticTag();
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return (Half)ReadInteger();

                case CborMajorType.NegativeInteger:
                    return (Half)(-1L - (long)ReadInteger());

                case CborMajorType.TextString:
                    ReadOnlySpan<byte> buffer = ReadSizeAndBytes(true);
                    if (!Utf8Parser.TryParse(buffer, out double value, out int bytesConsumed))
                    {
                        ThrowCbor($"Cannot parse half from {Encoding.ASCII.GetString(buffer)}");
                    }
                    return (Half)value;

                case CborMajorType.Primitive:
                    {
                        switch (header.Primitive)
                        {
                            case CborPrimitive.HalfFloat:
                                return InternalReadHalf();

                            case CborPrimitive.SingleFloat:
                                return (Half)InternalReadSingle();

                            case CborPrimitive.DoubleFloat:
                                return (Half)InternalReadDouble();

                            default:
                                ThrowCbor($"Invalid primitive {header.Primitive}");
                                return default; // Unreachable
                        }
                    }

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle()
        {
            SkipSemanticTag();
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return ReadInteger();

                case CborMajorType.NegativeInteger:
                    return -1L - (long)ReadInteger();

                case CborMajorType.TextString:
                    ReadOnlySpan<byte> buffer = ReadSizeAndBytes(true);
                    if (!Utf8Parser.TryParse(buffer, out float value, out int bytesConsumed))
                    {
                        ThrowCbor($"Cannot parse single from {Encoding.ASCII.GetString(buffer)}");
                    }
                    return value;

                case CborMajorType.Primitive:
                    {
                        switch (header.Primitive)
                        {
                            case CborPrimitive.HalfFloat:
                                return (float)InternalReadHalf();

                            case CborPrimitive.SingleFloat:
                                return InternalReadSingle();

                            case CborPrimitive.DoubleFloat:
                                return (float)InternalReadDouble();

                            default:
                                ThrowCbor($"Invalid primitive {header.Primitive}");
                                return default; // Unreachable
                        }
                    }

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            SkipSemanticTag();
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return ReadInteger();

                case CborMajorType.NegativeInteger:
                    return -1L - (long)ReadInteger();

                case CborMajorType.TextString:
                    ReadOnlySpan<byte> buffer = ReadSizeAndBytes(true);
                    if (!Utf8Parser.TryParse(buffer, out double value, out int bytesConsumed))
                    {
                        ThrowCbor($"Cannot parse double from {Encoding.ASCII.GetString(buffer)}");
                    }
                    return value;

                case CborMajorType.Primitive:
                    {
                        switch (header.Primitive)
                        {
                            case CborPrimitive.HalfFloat:
                                return (double)InternalReadHalf();

                            case CborPrimitive.SingleFloat:
                                return InternalReadSingle();

                            case CborPrimitive.DoubleFloat:
                                return InternalReadDouble();

                            default:
                                ThrowCbor($"Invalid primitive {header.Primitive}");
                                return default; // Unreachable
                        }
                    }

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        {
            SkipSemanticTag();
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return ReadInteger();

                case CborMajorType.NegativeInteger:
                    return -1L - (long)ReadInteger();

                case CborMajorType.TextString:
                    ReadOnlySpan<byte> buffer = ReadSizeAndBytes(true);
                    if (!Utf8Parser.TryParse(buffer, out decimal value, out int bytesConsumed))
                    {
                        ThrowCbor($"Cannot parse decimal from {Encoding.ASCII.GetString(buffer)}");
                    }
                    return value;

                case CborMajorType.Primitive:
                    {
                        switch (header.Primitive)
                        {
                            case CborPrimitive.HalfFloat:
                                return (decimal)(float)InternalReadHalf();

                            case CborPrimitive.SingleFloat:
                                return Convert.ToDecimal(InternalReadSingle());

                            case CborPrimitive.DoubleFloat:
                                return Convert.ToDecimal(InternalReadDouble());

                            case CborPrimitive.DecimalFloat:
                                return InternalReadDecimal();

                            default:
                                ThrowCbor($"Invalid primitive {header.Primitive}");
                                return default; // Unreachable
                        }
                    }

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        /// <summary>
        /// Reads an RFC 8949 §3.4.3 bignum - tag 2 (unsigned) or tag 3 (negative) over a byte string -
        /// or any basic integer, which the preferred serialization uses for values that fit in 64 bits.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ReadDecimal"/> and its neighbours this does not accept a text string. There
        /// is no span overload of <see cref="BigInteger.Parse(string)"/> on netstandard2.0, so accepting
        /// one would mean decoding to a <see cref="string"/> first and picking an encoding on a path
        /// nothing asks for; a text string is rejected rather than parsed on a guess.
        ///
        /// A negative bignum is exactly <c>-1 - n</c>, computed in <see cref="BigInteger"/> rather than
        /// in <see cref="long"/>. That also widens the basic-integer arm: major type 1 reaches -2^64,
        /// which no <see cref="long"/> holds, so <see cref="ReadInt64"/> rejects it as an invalid
        /// signed integer rather than reading it. Here it decodes.
        /// </remarks>
        public BigInteger ReadBigInteger()
        {
            bool isBignum = false;
            bool isNegativeBignum = false;

            // The whole tag stack, not one tag: since #183 every reader on this type reads through a
            // stack rather than stopping at the first, and a bignum under an outer tag would otherwise
            // be the one shape this reader alone refused. Foreign tags are skipped as they are
            // everywhere else, and the innermost bignum tag decides -- the tag nearest the data is the
            // one describing it.
            while (IsSemanticTag())
            {
                TryReadSemanticTag(out ulong tag);

                if (tag == UNSIGNED_BIGNUM_TAG || tag == NEGATIVE_BIGNUM_TAG)
                {
                    isBignum = true;
                    isNegativeBignum = tag == NEGATIVE_BIGNUM_TAG;
                }
            }

            if (isBignum)
            {
                Expect(CborMajorType.ByteString);

                // Big-endian, unsigned, and allowed to be empty - an empty magnitude is 0, so tag 2
                // reads as 0 and tag 3 as -1.
                BigInteger magnitude = BigIntegerFromBigEndianBytes(ReadSizeAndBytes(allowScratchBuffer: false));

                return isNegativeBignum ? BigInteger.MinusOne - magnitude : magnitude;
            }

            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return ReadInteger();

                case CborMajorType.NegativeInteger:
                    return BigInteger.MinusOne - ReadInteger();

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        /// <summary>
        /// Rebuilds a non-negative <see cref="BigInteger"/> from a big-endian magnitude.
        /// </summary>
        private static BigInteger BigIntegerFromBigEndianBytes(ReadOnlySpan<byte> bytes)
        {
#if NET8_0_OR_GREATER
            return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
#else
            // The netstandard2.0 constructor takes little-endian two's complement, so the bytes are
            // reversed and a zero appended: without that high byte a magnitude whose top bit is set -
            // 0x80 and up - would be read back as a negative number.
            byte[] littleEndian = new byte[bytes.Length + 1];

            for (int i = 0; i < bytes.Length; i++)
            {
                littleEndian[i] = bytes[bytes.Length - 1 - i];
            }

            return new BigInteger(littleEndian);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadSize()
        {
            if (GetHeader().AdditionalValue == INDEFINITE_LENGTH)
            {
                _state = CborReaderState.Data;
                return -1;
            }

            return (int)ReadInteger(int.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadMap<TC>(ICborMapReader<TC> mapReader, ref TC context)
        {
            EnterNestedItem();
            ReadBeginMap();

            int previousRemainingItemCount = _remainingItemCount;
            _remainingItemCount = ReadSize();

            mapReader.ReadBeginMap(_remainingItemCount, ref context);

            while (MoveNextMapItem())
            {
                mapReader.ReadMapItem(ref this, ref context);
            }

            _state = CborReaderState.Start;
            _remainingItemCount = previousRemainingItemCount;
            _depth--;
        }

        public bool MoveNextMapItem()
        {
            return MoveNextMapItem(ref _remainingItemCount);
        }

        public bool MoveNextMapItem(ref int remainingItemCount)
        {
            if (remainingItemCount == 0 || remainingItemCount < 0 && IsBreak())
            {
                return false;
            }

            remainingItemCount--;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<TC>(ICborArrayReader<TC> arrayReader, ref TC context)
        {
            EnterNestedItem();
            ReadBeginArray();

            int size = ReadSize();

            arrayReader.ReadBeginArray(size, ref context);

            while (size > 0 || size < 0 && !IsBreak())
            {
                arrayReader.ReadArrayItem(ref this, ref context);
                size--;
            }

            _state = CborReaderState.Start;
            _depth--;
        }

        /// <summary>
        /// Accounts for entering a nested map or array, and refuses to go deeper than
        /// <see cref="MaxDepth"/>. Bounds stack use on untrusted input, where a few bytes can
        /// describe arbitrarily deep nesting.
        /// </summary>
        /// <remarks>
        /// The matching decrement in <c>ReadMap</c>/<c>ReadArray</c> is deliberately not wrapped in a
        /// <c>finally</c>: a reader that has thrown is never read from again — it is a single-use
        /// <c>ref struct</c> the caller discards — so the depth left behind is unobservable, while an
        /// exception handler would make these methods ineligible for JIT inlining on the hot path.
        /// </remarks>
        private void EnterNestedItem()
        {
            if (++_depth > _maxDepth)
            {
                ThrowCbor(
                    $"CBOR nesting depth exceeded the configured maximum of {_maxDepth}. " +
                    $"Raise {nameof(CborOptions)}.{nameof(CborOptions.MaxDepth)} if the data is genuinely " +
                    "this deeply nested.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadUnsigned(ulong maxValue)
        {
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return ReadInteger(maxValue);

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadSigned(long maxValue)
        {
            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return (long)ReadInteger((ulong)maxValue);

                case CborMajorType.NegativeInteger:
                    return -1L - (long)ReadInteger((ulong)maxValue);

                default:
                    ThrowCbor($"Invalid major type {header.MajorType}");
                    return default; // Unreachable
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadInteger(ulong maxValue = ulong.MaxValue)
        {
            CborReaderHeader header = GetHeader();

            ulong value;

            switch (header.AdditionalValue)
            {
                // 8 bits
                case 24:
                    value = ReadBytes(1)[0];
                    break;

                // 16 bits
                case 25:
                    value = BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2));
                    break;

                // 32 bits
                case 26:
                    value = BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4));
                    break;

                // 64 bits
                case 27:
                    value = BinaryPrimitives.ReadUInt64BigEndian(ReadBytes(8));
                    break;

                case 28:
                case 29:
                case 30:
                case 31:
                    ThrowCbor($"Unexpected additional value {header.AdditionalValue}");
                    value = default; // Unreachable
                    break;

                default:
                    value = header.AdditionalValue;
                    _state = CborReaderState.Data;
                    break;
            }

            if (value > maxValue)
            {
                ThrowCbor("Invalid signed integer");
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Half InternalReadHalf()
        {
            ReadOnlySpan<byte> bytes = ReadBytes(2);
            return HalfHelpers.ReadHalf(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float InternalReadSingle()
        {
            ReadOnlySpan<byte> bytes = ReadBytes(4);

            if (BitConverter.IsLittleEndian)
            {
                uint value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
                unsafe
                {
                    return *(float*)(&value);
                }
            }
            else
            {
                return MemoryMarshal.Read<float>(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double InternalReadDouble()
        {
            ReadOnlySpan<byte> bytes = ReadBytes(8);

            if (BitConverter.IsLittleEndian)
            {
                ulong value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
                unsafe
                {
                    return *(double*)(&value);
                }
            }
            else
            {
                return MemoryMarshal.Read<double>(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private decimal InternalReadDecimal()
        {
            ReadOnlySpan<byte> bytes = ReadBytes(16);

            if (BitConverter.IsLittleEndian)
            {
                int i1 = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(0, 4));
                int i0 = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(4, 4));
                int i2 = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(8, 4));
                int i3 = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(12, 4));

                return new decimal(new int[] { i0, i1, i2, i3 });
            }
            else
            {
                return MemoryMarshal.Read<decimal>(bytes);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadSizeAndBytes(bool allowScratchBuffer)
        {
            CborMajorType majorType = GetHeader().MajorType;
            int size = ReadSize();

            if (size == -1)
            {
                return ReadChunks(majorType);
            }

            return ReadBytes(size, allowScratchBuffer);
        }

        /// <summary>
        /// Reads an RFC 8949 section 3.2.3 indefinite-length string: zero or more definite-length
        /// chunks of <paramref name="majorType"/>, terminated by a break, concatenated into one value.
        /// </summary>
        /// <remarks>
        /// The chunk boundaries carry no meaning — an indefinite-length string denotes exactly the
        /// concatenation of its chunks, so a caller cannot tell one from the definite-length string of
        /// the same content, and neither can a UTF-8 decode: a multi-byte character may straddle a
        /// chunk boundary, which is why the bytes are joined before decoding rather than per chunk.
        /// <para>
        /// The result is a freshly allocated array rather than a slice of the input or the scratch
        /// buffer, because the pieces are not contiguous in either. That allocation is confined to
        /// documents that actually use the encoding; a definite-length string does not reach here.
        /// </para>
        /// </remarks>
        private ReadOnlySpan<byte> ReadChunks(CborMajorType majorType)
        {
            byte[] joined = ReadChunksArray(majorType, out int length);

            return new ReadOnlySpan<byte>(joined, 0, length);
        }

        /// <inheritdoc cref="ReadChunks"/>
        /// <remarks>
        /// The buffer grows by doubling, so it is usually longer than the value; the caller is handed
        /// the length and takes a window over it rather than an exactly sized array. Trimming here
        /// instead would copy the whole value again on every overshoot, on the two paths that read a
        /// string rather than the one that reads a sequence.
        /// <para>
        /// A plain array rather than <c>ByteBufferWriter</c>, which is the obvious tool and the wrong
        /// one: it rents from <see cref="System.Buffers.ArrayPool{T}"/>, and the result outlives this
        /// call, so a pooled buffer would be returned and handed to another caller underneath the one
        /// holding it.
        /// </para>
        /// </remarks>
        private byte[] ReadChunksArray(CborMajorType majorType, out int length)
        {
            byte[] joined = Array.Empty<byte>();
            length = 0;

            while (!IsBreak())
            {
                ReadOnlySpan<byte> chunk = ReadChunk(majorType);

                if (length + chunk.Length > joined.Length)
                {
                    Array.Resize(ref joined, Math.Max(length + chunk.Length, joined.Length * 2));
                }

                chunk.CopyTo(joined.AsSpan(length));
                length += chunk.Length;
            }

            ConsumeBreak();

            return joined;
        }

        /// <summary>
        /// One chunk of an indefinite-length string. A chunk must be a definite-length string of the
        /// same major type: anything else, including a nested indefinite-length string, is malformed
        /// rather than something to interpret.
        /// </summary>
        private ReadOnlySpan<byte> ReadChunk(CborMajorType majorType)
        {
            CborReaderHeader header = GetHeader();

            if (header.MajorType != majorType)
            {
                ThrowCbor($"Expected major type {majorType} in an indefinite-length string chunk, found {header.MajorType}");
            }

            int size = ReadSize();

            if (size == -1)
            {
                ThrowCbor("An indefinite-length string cannot contain another indefinite-length string");
            }

            // Never the scratch buffer: it is a single shared buffer, so the next chunk would overwrite
            // this one before it has been copied out.
            return ReadBytes(size, allowScratchBuffer: false);
        }

        /// <summary>
        /// Settles the reader past a break whose header <see cref="IsBreak"/> has already read.
        /// </summary>
        /// <remarks>
        /// Settles to <see cref="CborReaderState.Data"/> where <c>SkipArray</c> and <c>SkipMap</c>
        /// settle to <see cref="CborReaderState.Start"/>. Equivalent in effect: only <c>Header</c> is
        /// ever tested for, by <see cref="GetHeader"/>, and both values mean "the cached header has
        /// been consumed". <c>Data</c> matches what <see cref="SkipDataItem"/> does for a break.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConsumeBreak()
        {
            GetHeader();
            _state = CborReaderState.Data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadDataItem(bool advance = true)
        {
            int positionBefore = _currentPos;
            var stateBefore = _state;
            
            int headerOffset = _state == CborReaderState.Header ? 1 : 0;
            int currentDataItemPos = _currentPos - headerOffset;

            SkipDataItem();

            int size = _currentPos - currentDataItemPos;

            if (!advance)
            {
                _currentPos = positionBefore;
                _state = stateBefore;
            }

            return _buffer.Slice(currentDataItemPos, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySequence<byte> ReadSizeAndSequence()
        {
            CborMajorType majorType = GetHeader().MajorType;
            int size = ReadSize();

            if (size == -1)
            {
                // The chunks are not contiguous in the input, so there is no slice of it to hand back;
                // the joined copy becomes the sequence, windowed to the length actually read.
                byte[] joined = ReadChunksArray(majorType, out int length);

                return new ReadOnlySequence<byte>(joined, 0, length);
            }

            ExpectLength(size);

            ReadOnlySequence<byte> sequence;

            if (_sequence.HasValue)
            {
                sequence = _sequence.Value.Slice(_currentPos, size);
            }
            else
            {
                var buffer = new byte[size];
                _buffer.Slice(_currentPos, size).CopyTo(buffer);
                sequence = new ReadOnlySequence<byte>(buffer);
            }

            Advance(size);

            return sequence;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CborReaderHeader GetHeader()
        {
            if (_state == CborReaderState.Header)
            {
                return _header;
            }

            byte headerByte = ReadBytes(1)[0];
            _header.MajorType = (CborMajorType)((headerByte >> 5) & 0x07);
            _header.AdditionalValue = (byte)(headerByte & 0x1f);

            if (_header.MajorType > CborMajorType.Max)
            {
                ThrowCbor($"Invalid major type {_header.MajorType}");
            }

            _state = CborReaderState.Header;

            return _header;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadBytes(int length, bool allowScratchBuffer = true)
        {
            ExpectLength(length);
            ReadOnlySpan<byte> slice = GetBytes(length, allowScratchBuffer);
            Advance(length);
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CborMajorType GetCurrentMajorType()
        {
            ExpectLength(1);
            byte headerByte = GetBytes(1)[0];
            CborMajorType majorType = (CborMajorType)((headerByte >> 5) & 0x07);

            if (majorType > CborMajorType.Max)
            {
                ThrowCbor($"Invalid major type {majorType}");
            }

            return majorType;
        }

        /// <summary>
        /// Steps over every semantic tag in front of the next data item, leaving the reader on the
        /// item's own header.
        /// </summary>
        /// <remarks>
        /// A loop rather than a single skip because RFC 8949 §3.4 lets tags stack - tag 1 over a
        /// bignum is <c>C1 C2 ...</c> - and a caller that does not care about tags cares even less
        /// about how many there were. Stopping after one left the second tag standing where the item's
        /// header was expected, which every caller then misread in its own way: the integer reads
        /// rejected it as a major type, and <see cref="SkipDataItem"/> treated the item as already
        /// skipped and returned with it still in the buffer, desynchronising everything after it.
        /// <para>
        /// The tag is consumed by reading its argument, not by advancing a fixed number of bytes, so
        /// a multi-byte tag head such as <c>D8 64</c> is stepped over whole.
        /// </para>
        /// <para>
        /// Every tag but the outermost is lost, since nothing here has anywhere to put it - a
        /// converter that wants its tag reads it with <see cref="TryReadSemanticTag"/> before the
        /// value, and <see cref="ObjectModel.CborValue.SemanticTag"/> holds one tag. A lost tag is the
        /// intended shortfall; a value decoded from the wrong bytes was not.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSemanticTag()
        {
            while (Accept(CborMajorType.SemanticTag))
            {
                ReadInteger();
                _state = CborReaderState.Data;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SkipDataItem()
        {
            SkipSemanticTag();

            CborReaderHeader header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                case CborMajorType.NegativeInteger:
                    ReadInteger();
                    break;

                case CborMajorType.ByteString:
                case CborMajorType.TextString:
                    SkipSizeAndBytes();
                    break;

                case CborMajorType.Array:
                    SkipArray();
                    break;

                case CborMajorType.Map:
                    SkipMap();
                    break;

                case CborMajorType.SemanticTag:
                    // Impossible - SkipSemanticTag above takes the whole stack. It used to take one,
                    // which made this arm reachable for a nested tag and this method return with the
                    // tagged item unread, leaving the buffer one item out of step.
                    break;

                case CborMajorType.Primitive:
                    switch (header.Primitive)
                    {
                        case CborPrimitive.False:
                        case CborPrimitive.True:
                        case CborPrimitive.Null:
                        case CborPrimitive.Undefined:
                        case CborPrimitive.SimpleValue:
                        case CborPrimitive.Break:
                            _state = CborReaderState.Data;
                            break;

                        case CborPrimitive.HalfFloat:
                            Advance(2);
                            break;

                        case CborPrimitive.SingleFloat:
                            Advance(4);
                            break;

                        case CborPrimitive.DoubleFloat:
                            Advance(8);
                            break;

                        case CborPrimitive.DecimalFloat:
                            Advance(16);
                            break;
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSizeAndBytes()
        {
            CborMajorType majorType = GetHeader().MajorType;
            int size = ReadSize();

            if (size == -1)
            {
                // Skipping still validates the chunks: a malformed indefinite-length string must not
                // pass unnoticed just because its value was not wanted.
                while (!IsBreak())
                {
                    ReadChunk(majorType);
                }

                ConsumeBreak();
                return;
            }

            ExpectLength(size);
            Advance(size);
        }

        /// <remarks>
        /// Guarded by <see cref="EnterNestedItem"/> for the same reason <see cref="ReadArray"/> is, and
        /// against the same budget: skipping recurses through <see cref="SkipDataItem"/> exactly as
        /// reading does, so nesting reached by way of a member the target type does not declare costs
        /// the same stack as nesting the type asks for. Counting the two separately would let a
        /// document spend <see cref="MaxDepth"/> twice.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipArray()
        {
            EnterNestedItem();

            int size = ReadSize();

            while (size > 0 || size < 0 && !IsBreak())
            {
                SkipDataItem();
                size--;
            }

            _state = CborReaderState.Start;
            _depth--;
        }

        /// <inheritdoc cref="SkipArray"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipMap()
        {
            EnterNestedItem();

            int size = ReadSize();

            while (size > 0 || size < 0 && !IsBreak())
            {
                SkipDataItem();
                SkipDataItem();
                size--;
            }

            _state = CborReaderState.Start;
            _depth--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Accept(CborPrimitive primitive)
        {
            if (Accept(CborMajorType.Primitive) && _header.Primitive == primitive)
            {
                _state = CborReaderState.Data;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Expect(CborPrimitive primitive)
        {
            if (!Accept(primitive))
            {
                ThrowCbor($"Expected primitive {primitive} ({(byte)primitive})");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Accept(CborMajorType majorType)
        {
            return GetHeader().MajorType == majorType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Expect(CborMajorType majorType)
        {
            if (!Accept(majorType))
            {
                ThrowCbor($"Expected major type {majorType} ({(byte)majorType})");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpectLength(int length)
        {
            int remaining = _length - _currentPos;
            if (remaining < length)
            {
                ThrowCbor($"Unexpected end of buffer");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> GetBytes(int length, bool allowScratchBuffer = true)
        {
            if (!_sequence.HasValue)
            {
                return _buffer.Slice(_currentPos, length);
            }

            if (_sequenceReader.UnreadSpan.Length >= length)
            {
                return _sequenceReader.UnreadSpan.Slice(0, length);
            }
            else if (allowScratchBuffer && length <= SCRATCH_BUFFER_SIZE)
            {
                byte[] scratchBuffer = _scratchBuffer ??= new byte[SCRATCH_BUFFER_SIZE];
                Span<byte> span = scratchBuffer.AsSpan(0, length);
                _sequenceReader.TryCopyTo(span);
                return span;
            }
            else
            {
                byte[] array = new byte[length];
                _sequenceReader.TryCopyTo(array);
                return array;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance(int length)
        {
            if (_state == CborReaderState.Header)
            {
                _state = CborReaderState.Data;
            }
            _currentPos += length;

            if (_sequence.HasValue)
            {
                _sequenceReader.Advance(length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CborException BuildException(string message)
        {
            return new CborException($"[{_currentPos}] {message}");
        }

        private void ThrowCbor(string message)
        {
            throw BuildException(message);
        }

    }
}
