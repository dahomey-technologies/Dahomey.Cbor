using Dahomey.Cbor.Util;
using System;
using System.Buffers.Binary;
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
        Break
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

    public ref struct CborReader
    {
        private enum State
        {
            Start,
            Header,
            Data
        }

        [StructLayout(LayoutKind.Explicit, Size = 2)]
        private struct Header
        {
            [FieldOffset(0)]
            public CborMajorType MajorType;

            [FieldOffset(1)]
            public byte AdditionalValue;

            [FieldOffset(1)]
            public CborPrimitive Primitive;
        }

        private const int CHUNK_SIZE = 1024;
        private const byte INDEFINITE_LENGTH = 31;

        private ReadOnlySpan<byte> _buffer;
        private int _currentPos;
        private State _state;
        private Header _header;

        public CborOptions Options { get; }

        public CborReader(ReadOnlySpan<byte> buffer, CborOptions options = null)
        {
            _buffer = buffer;
            Options = options ?? CborOptions.Default;
            _currentPos = 0;
            _state = State.Start;
            _header = new Header();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadSemanticTag(out ulong semanticTag)
        {
            if (Accept(CborMajorType.SemanticTag))
            {
                semanticTag = ReadInteger();
                _state = State.Data;
                return true;
            }

            semanticTag = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CborDataItemType GetCurrentDataItemType()
        {
            SkipSemanticTag();
            Header header = GetHeader();

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
                    Advance();
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

                        case CborPrimitive.Break:
                            return CborDataItemType.Break;

                        default:
                            throw BuildException("Primitive not supported");
                    }

                default:
                    throw BuildException("Major type not supported");
            }
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
        public string ReadString()
        {
            if (ReadNull())
            {
                return null;
            }

            Expect(CborMajorType.TextString);
            return Encoding.UTF8.GetString(ReadSizeAndBytes());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadRawString()
        {
            if (ReadNull())
            {
                return null;
            }

            Expect(CborMajorType.TextString);
            return ReadSizeAndBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadByteString()
        {
            SkipSemanticTag();
            Expect(CborMajorType.ByteString);
            return ReadSizeAndBytes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle()
        {
            SkipSemanticTag();
            Expect(CborMajorType.Primitive);

            if (Accept(CborPrimitive.SingleFloat))
            {
                return InternalReadSingle();
            }

            if (Accept(CborPrimitive.HalfFloat))
            {
                return (float)InternalReadHalfFloat();
            }

            Expect(CborPrimitive.DoubleFloat);
            return (float)InternalReadDouble();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            SkipSemanticTag();
            Expect(CborMajorType.Primitive);

            if (Accept(CborPrimitive.DoubleFloat))
            {
                return InternalReadDouble();
            }

            if (Accept(CborPrimitive.HalfFloat))
            {
                throw BuildException("Half precision floats are not supported");
            }

            Expect(CborPrimitive.SingleFloat);
            return InternalReadSingle();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadSize()
        {
            if (GetHeader().AdditionalValue == INDEFINITE_LENGTH)
            {
                _state = State.Data;
                return -1;
            }

            return (int)ReadInteger(int.MaxValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadMap<TC>(ICborMapReader<TC> mapReader, ref TC context)
        {
            ReadBeginMap();

            int size = ReadSize();

            mapReader.ReadBeginMap(size, ref context);

            while (size > 0 || size < 0 && GetCurrentDataItemType() != CborDataItemType.Break)
            {
                mapReader.ReadMapItem(ref this, ref context);
                size--;
            }

            _state = State.Start;
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

            _state = State.Start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadUnsigned(ulong maxValue)
        {
            Header header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return ReadInteger(maxValue);

                default:
                    throw new CborException($"Invalid major type {header.MajorType}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadSigned(long maxValue)
        {
            Header header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                    return (long)ReadInteger((ulong)maxValue);

                case CborMajorType.NegativeInteger:
                    return -1L - (long)ReadInteger((ulong)maxValue);

                default:
                    throw new CborException($"Invalid major type {header.MajorType}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadInteger(ulong maxValue = ulong.MaxValue)
        {
            Header header = GetHeader();

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
                    throw BuildException($"Unexpected additional value {header.AdditionalValue}");

                default:
                    value = header.AdditionalValue;
                    _state = State.Data;
                    break;
            }

            if (value > maxValue)
            {
                throw BuildException("Invalid signed integer");
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double InternalReadHalfFloat()
        {
            ReadOnlySpan<byte> bytes = ReadBytes(2);

            int half = (bytes[0] << 8) + bytes[1];
            int exp = (half >> 10) & 0x1f;
            int mant = half & 0x3ff;
            double val;
            if (exp == 0)
            {
                val = MathUtil.LdExp(mant, -24);
            }
            else if (exp != 31)
            {
                val = MathUtil.LdExp(mant + 1024, exp - 25);
            }
            else
            {
                val = mant == 0 ? double.PositiveInfinity : double.NaN;
            }
            return (half & 0x8000) != 0 ? -val : val;
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
        private ReadOnlySpan<byte> ReadSizeAndBytes()
        {
            int size = ReadSize();

            if (size == -1)
            {
                throw new NotSupportedException();
            }

            return ReadBytes(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Header GetHeader()
        {
            if (_state == State.Header)
            {
                return _header;
            }

            ExpectLength(1);

            _header.MajorType = (CborMajorType)((_buffer[0] >> 5) & 0x07);
            _header.AdditionalValue = (byte)(_buffer[0] & 0x1f);

            if (_header.MajorType > CborMajorType.Max)
            {
                throw new CborException($"Invalid major type {_header.MajorType}");
            }

            Advance();
            _state = State.Header;

            return _header;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlySpan<byte> ReadBytes(int length)
        {
            ExpectLength(length);
            ReadOnlySpan<byte> slice = _buffer.Slice(0, length);
            Advance(length);
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CborMajorType GetCurrentMajorType()
        {
            ExpectLength(1);

            CborMajorType majorType = (CborMajorType)((_buffer[0] >> 5) & 0x07);

            if (majorType > CborMajorType.Max)
            {
                throw new CborException($"Invalid major type {majorType}");
            }

            return majorType;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipSemanticTag()
        {
            if (Accept(CborMajorType.SemanticTag))
            {
                ReadInteger();
                _state = State.Data;
                return;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SkipDataItem()
        {
            Header header = GetHeader();

            switch (header.MajorType)
            {
                case CborMajorType.PositiveInteger:
                case CborMajorType.NegativeInteger:
                    ReadInteger();
                    break;

                case CborMajorType.ByteString:
                case CborMajorType.TextString:
                    ReadSizeAndBytes();
                    break;

                case CborMajorType.Array:
                    SkipArray();
                    break;

                case CborMajorType.Map:
                    SkipMap();
                    break;

                case CborMajorType.SemanticTag:
                    _state = State.Data;
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
                            _state = State.Data;
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
                    }
                    break;
            }
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

            _state = State.Start;
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

            _state = State.Start;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Accept(CborPrimitive primitive)
        {
            if (Accept(CborMajorType.Primitive) && _header.Primitive == primitive)
            {
                _state = State.Data;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Expect(CborPrimitive primitive)
        {
            if (!Accept(primitive))
            {
                throw BuildException($"Expected primitive {primitive} ({(byte)primitive})");
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
                throw BuildException($"Expected major type {majorType} ({(byte)majorType})");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpectLength(int length)
        {
            if (_buffer.Length < length)
            {
                throw BuildException($"Unexpected end of buffer");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance(int length = 1)
        {
            if (_state == State.Header)
            {
                _state = State.Data;
            }
            _buffer = _buffer.Slice(length);
            _currentPos += length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CborException BuildException(string message)
        {
            return new CborException($"[{_currentPos}] {message}");
        }
    }
}
