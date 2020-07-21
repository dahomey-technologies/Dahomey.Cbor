using Dahomey.Cbor.Util;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Dahomey.Cbor.Serialization
{
    public interface ICborMapWriter<TC>
    {
        int GetMapSize(ref TC context);
        bool WriteMapItem(ref CborWriter writer, ref TC context);
    }

    public interface ICborArrayWriter<TC>
    {
        int GetArraySize(ref TC context);
        bool WriteArrayItem (ref CborWriter writer, ref TC context);
    }

    public ref struct CborWriter
    {
        private const byte INDEFINITE_LENGTH = 31;

        private IBufferWriter<byte> _bufferWriter;

        public CborOptions Options { get; }

        public CborWriter(IBufferWriter<byte> bufferWriter, CborOptions? options = null)
        {
            _bufferWriter = bufferWriter;
            Options = options ?? CborOptions.Default;
        }

        public void WriteSemanticTag(ulong semanticTag)
        {
            WriteInteger(CborMajorType.SemanticTag, semanticTag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNull()
        {
            WritePrimitive(CborPrimitive.Null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolean(bool value)
        {
            WritePrimitive(value ? CborPrimitive.True : CborPrimitive.False);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value)
        {
            WriteSigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            WriteUnsigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(short value)
        {
            WriteSigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            WriteUnsigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value)
        {
            WriteSigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            WriteSigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value)
        {
            WriteSigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            WriteUnsigned(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteHalf(Half value)
        {
            InternalWriteHalf(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingle(float value)
        {
            if (float.IsNaN(value))
            {
                InternalWriteHalf(Half.NaN);
                return;
            }
            else if (float.IsNegativeInfinity(value))
            {
                InternalWriteHalf(Half.NegativeInfinity);
                return;
            }
            else if (float.IsPositiveInfinity(value))
            {
                InternalWriteHalf(Half.PositiveInfinity);
                return;
            }

            Half half = (Half)value; 
            if (half == value)
            {
                InternalWriteHalf(half);
                return;
            }

            InternalWriteSingle(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            if (double.IsNaN(value))
            {
                InternalWriteHalf(Half.NaN);
                return;
            }
            else if (double.IsNegativeInfinity(value))
            {
                InternalWriteHalf(Half.NegativeInfinity);
                return;
            }
            else if (double.IsPositiveInfinity(value))
            {
                InternalWriteHalf(Half.PositiveInfinity);
                return;
            }

            Half half = (Half)value;
            if (half == value)
            {
                InternalWriteHalf(half);
                return;
            }

            float single = (float)value;
            if (single == value)
            {
                InternalWriteSingle(single);
                return;
            }

            InternalWriteDouble(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimal(decimal value)
        {
            InternalWriteDecimal(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(string? value)
        {
            if (value == null)
            {
                WriteNull();
                return;
            }

            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInteger(CborMajorType.TextString, (ulong)byteCount);

            if (byteCount == 0)
            {
                return;
            }

            Span<byte> bytes = _bufferWriter.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value.AsSpan(), bytes);
            _bufferWriter.Advance(byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteString(ReadOnlySpan<byte> value)
        {
            if (value == null)
            {
                WriteNull();
                return;
            }

            WriteInteger(CborMajorType.TextString, (ulong)value.Length);
            _bufferWriter.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteString(ReadOnlySpan<byte> value)
        {
            WriteInteger(CborMajorType.ByteString, (ulong)value.Length);
            _bufferWriter.Write(value);
        }

        public void WriteBeginMap(int size)
        {
            WriteSize(CborMajorType.Map, size);
        }

        public void WriteEndMap(int size)
        {
            if (size == -1)
            {
                WritePrimitive(CborPrimitive.Break);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteMap<TC>(ICborMapWriter<TC> mapWriter, ref TC context)
        {
            int size = mapWriter.GetMapSize(ref context);
            WriteBeginMap(size);
            while (mapWriter.WriteMapItem(ref this, ref context));
            WriteEndMap(size);
        }

        public void WriteBeginArray(int size)
        {
            WriteSize(CborMajorType.Array, size);
        }

        public void WriteEndArray(int size)
        {
            if (size == -1)
            {
                WritePrimitive(CborPrimitive.Break);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArray<TC>(ICborArrayWriter<TC> arrayWriter, ref TC context)
        {
            int size = arrayWriter.GetArraySize(ref context);
            WriteBeginArray(size);
            while(arrayWriter.WriteArrayItem(ref this, ref context));
            WriteEndArray(size);
        }

        private void WriteSize(CborMajorType majorType, int size)
        {
            if (size >= 0)
            {
                WriteInteger(majorType, (ulong)size);
            }
            else
            {
                WriteHeader(majorType, INDEFINITE_LENGTH);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSigned(long value)
        {
            if (value >= 0)
            {
                WriteUnsigned((ulong)value);
                return;
            }

            WriteInteger(CborMajorType.NegativeInteger, (ulong)(-1 - value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnsigned(ulong value)
        {
            WriteInteger(CborMajorType.PositiveInteger, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteInteger(CborMajorType majorType, ulong value)
        {
            if (value <= 23)
            {
                WriteHeader(majorType, (byte)value);
            }
            else if (value <= byte.MaxValue)
            {
                WriteHeader(majorType, 24);
                WriteRawByte((byte)value);
            }
            else if (value <= ushort.MaxValue)
            {
                WriteHeader(majorType, 25);
                Span<byte> bytes = _bufferWriter.GetSpan(2);
                BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
                _bufferWriter.Advance(2);
            }
            else if (value <= uint.MaxValue)
            {
                WriteHeader(majorType, 26);
                Span<byte> bytes = _bufferWriter.GetSpan(4);
                BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);
                _bufferWriter.Advance(4);
            }
            else
            {
                WriteHeader(majorType, 27);
                Span<byte> bytes = _bufferWriter.GetSpan(8);
                BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
                _bufferWriter.Advance(8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalWriteHalf(Half value)
        {
            if (value.IsNaN)
            {
                value = Half.ToHalf(0x7e00);
            }

            WritePrimitive(CborPrimitive.HalfFloat);

            Span<byte> bytes = _bufferWriter.GetSpan(2);
            value.GetBytes(bytes);
            _bufferWriter.Advance(2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalWriteSingle(float value)
        {
            WritePrimitive(CborPrimitive.SingleFloat);

            Span<byte> bytes = _bufferWriter.GetSpan(4);

            if (BitConverter.IsLittleEndian)
            {
                uint uintValue;
                unsafe
                {
                    uintValue = *(uint*)(&value);
                }
                BinaryPrimitives.WriteUInt32BigEndian(bytes, uintValue);
            }
            else
            {
                MemoryMarshal.Write(bytes, ref value);
            }

            _bufferWriter.Advance(4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalWriteDouble(double value)
        {
            WritePrimitive(CborPrimitive.DoubleFloat);

            Span<byte> bytes = _bufferWriter.GetSpan(8);

            if (BitConverter.IsLittleEndian)
            {
                ulong ulongValue;
                unsafe
                {
                    ulongValue = *(ulong*)(&value);
                }
                BinaryPrimitives.WriteUInt64BigEndian(bytes, ulongValue);
            }
            else
            {
                MemoryMarshal.Write(bytes, ref value);
            }

            _bufferWriter.Advance(8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalWriteDecimal(decimal value)
        {
            WritePrimitive(CborPrimitive.DecimalFloat);

            Span<byte> bytes = _bufferWriter.GetSpan(16);
            var span = _bufferWriter.GetSpan(16);

            if (BitConverter.IsLittleEndian)
            {
                int[] bits = decimal.GetBits(value);

                BinaryPrimitives.WriteInt32BigEndian(span.Slice(0, 4), bits[1]);
                BinaryPrimitives.WriteInt32BigEndian(span.Slice(4, 4), bits[0]);
                BinaryPrimitives.WriteInt32BigEndian(span.Slice(8, 4), bits[2]);
                BinaryPrimitives.WriteInt32BigEndian(span.Slice(12, 4), bits[3]);
            }
            else
            {
                MemoryMarshal.Write(span, ref value);
            }

            _bufferWriter.Advance(16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WritePrimitive(CborPrimitive primitive)
        {
            WriteHeader(CborMajorType.Primitive, (byte)primitive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteHeader(CborMajorType majorType, byte additionalValue)
        {
            byte header = (byte)(((byte)majorType) << 5 | (additionalValue & 0x1f));
            WriteRawByte(header);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteRawByte(byte b)
        {
            Span<byte> buffer = _bufferWriter.GetSpan(1);
            buffer[0] = b;
            _bufferWriter.Advance(1);
        }
    }
}
