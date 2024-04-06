using Dahomey.Cbor.Util;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
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

        private ReadOnlySpan<byte> _buffer;
        private ReadOnlySequence<byte>? _sequence;
        private int _currentPos;
        private int _length;
        private SequenceReader<byte> _sequenceReader;
        private CborReaderState _state;
        private CborReaderHeader _header;
        private int _remainingItemCount;
        private byte[]? _scratchBuffer;

        public bool DataAvailable => _currentPos < _length;

        public ReadOnlySpan<byte> Buffer => _sequence.HasValue
            ? throw new InvalidOperationException("Buffer is not available when reader is operating on a sequence buffer")
            : _buffer.Slice(_currentPos);

        public CborReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _sequence = null;
            _currentPos = 0;
            _length = buffer.Length;
            _sequenceReader = default;
            _state = CborReaderState.Start;
            _header = new CborReaderHeader();
            _remainingItemCount = 0;
            _scratchBuffer = null;
        }

        public CborReader(ReadOnlySequence<byte> buffer)
        {
            _buffer = ReadOnlySpan<byte>.Empty;
            _sequence = buffer;
            _currentPos = 0;
            _length = (int)buffer.Length;
            _sequenceReader = new SequenceReader<byte>(buffer);
            _state = CborReaderState.Start;
            _header = new CborReaderHeader();
            _remainingItemCount = 0;
            _scratchBuffer = null;
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
                    Advance(1);
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
                    return (Half)(- 1L - (long)ReadInteger());

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
        }

        public bool MoveNextMapItem()
        {
            return MoveNextMapItem(ref _remainingItemCount);
        }

        public bool MoveNextMapItem(ref int remainingItemCount)
        {
            if (remainingItemCount == 0 || remainingItemCount < 0 && GetCurrentDataItemType() == CborDataItemType.Break)
            {
                return false;
            }

            remainingItemCount--;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadArray<TC>(ICborArrayReader<TC> arrayReader, ref TC context)
        {
            ReadBeginArray();

            int size = ReadSize();

            arrayReader.ReadBeginArray(size, ref context);

            while (size > 0 || size < 0 && GetCurrentDataItemType() != CborDataItemType.Break)
            {
                arrayReader.ReadArrayItem(ref this, ref context);
                size--;
            }

            _state = CborReaderState.Start;
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
            int size = ReadSize();

            if (size == -1)
            {
                ThrowNotSupported();
            }

            return ReadBytes(size, allowScratchBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadDataItem()
        {
            int headerOffset = _state == CborReaderState.Header ? 1 : 0;
            int currentDataItemPos = _currentPos - headerOffset;

            SkipDataItem();

            int size = _currentPos - currentDataItemPos;

            return _buffer.Slice(currentDataItemPos, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySequence<byte> ReadSizeAndSequence()
        {
            int size = ReadSize();

            if (size == -1)
            {
                ThrowNotSupported();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSemanticTag()
        {
            if (Accept(CborMajorType.SemanticTag))
            {
                ReadInteger();
                _state = CborReaderState.Data;
                return;
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
                    // Impossible - already skipped
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
            int size = ReadSize();

            if (size == -1)
            {
                ThrowNotSupported();
            }

            ExpectLength(size);
            Advance(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipArray()
        {
            int size = ReadSize();

            while (size > 0 || size < 0 && GetCurrentDataItemType() != CborDataItemType.Break)
            {
                SkipDataItem();
                size--;
            }

            _state = CborReaderState.Start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipMap()
        {
            int size = ReadSize();

            while (size > 0 || size < 0 && GetCurrentDataItemType() != CborDataItemType.Break)
            {
                SkipDataItem();
                SkipDataItem();
                size--;
            }

            _state = CborReaderState.Start;
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

        private void ThrowNotSupported()
        {
            throw new NotSupportedException();
        }
    }
}
