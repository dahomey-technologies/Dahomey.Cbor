using Dahomey.Cbor.Serialization.Converters;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Dahomey.Cbor.Serialization
{
    public interface ICborMapWriter<TC>
    {
        int GetSize(ref TC context);
        bool WriteMapEntry(ref CborWriter writer, ref TC context);
    }

    public ref struct CborWriter
    {
        private IBufferWriter<byte> _bufferWriter;

        public CborSerializationSettings Settings { get; }

        public CborWriter(IBufferWriter<byte> bufferWriter, CborSerializationSettings settings = null)
        {
            _bufferWriter = bufferWriter;
            Settings = settings ?? CborSerializationSettings.Default;
        }

        public void WriteNull()
        {
            WritePrimitive(CborPrimitive.Null);
        }

        public void WriteBoolean(bool value)
        {
            WritePrimitive(value ? CborPrimitive.True : CborPrimitive.False);
        }

        public void WriteSByte(sbyte value)
        {
            WriteSigned(value);
        }

        public void WriteByte(byte value)
        {
            WriteUnsigned(value);
        }

        public void WriteInt16(short value)
        {
            WriteSigned(value);
        }

        public void WriteUInt16(ushort value)
        {
            WriteUnsigned(value);
        }

        public void WriteInt32(int value)
        {
            WriteSigned(value);
        }

        public void WriteUInt32(uint value)
        {
            WriteSigned(value);
        }

        public void WriteInt64(long value)
        {
            WriteSigned(value);
        }

        public void WriteUInt64(ulong value)
        {
            WriteUnsigned(value);
        }

        public void WriteSingle(float value)
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

        public void WriteDouble(double value)
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

        public void WriteString(string value)
        {
            if (value == null)
            {
                WriteNull();
                return;
            }

            WriteInteger(CborMajorType.TextString, (ulong)value.Length);

            if (value.Length == 0)
            {
                return;
            }

            int byteCount = Encoding.UTF8.GetByteCount(value);
            Span<byte> bytes = _bufferWriter.GetSpan(byteCount);
            Encoding.UTF8.GetBytes(value.AsSpan(), bytes);
            _bufferWriter.Advance(byteCount);
        }

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

        public void WriteByteString(ReadOnlySpan<byte> value)
        {
            WriteInteger(CborMajorType.ByteString, (ulong)value.Length);
            _bufferWriter.Write(value);
        }

        public void WriteMap<TC>(ICborMapWriter<TC> mapWriter, ref TC context)
        {
            int size = mapWriter.GetSize(ref context);
            WriteInteger(CborMajorType.Map, (ulong)size);

            while (mapWriter.WriteMapEntry(ref this, ref context));
        }

        public void WriteArray<T>(ICollection<T> collection, ICborValueWriter<T> itemWriter)
        {
            if (collection == null)
            {
                WriteNull();
                return;
            }

            WriteInteger(CborMajorType.Array, (ulong)collection.Count);

            foreach (T item in collection)
            {
                itemWriter.Write(ref this, item);
            }
        }

        private void WriteSigned(long value)
        {
            if (value >= 0)
            {
                WriteUnsigned((ulong)value);
                return;
            }

            WriteInteger(CborMajorType.NegativeInteger, (ulong)(-1 - value));
        }

        private void WriteUnsigned(ulong value)
        {
            WriteInteger(CborMajorType.PositiveInteger, value);
        }

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

        private void WritePrimitive(CborPrimitive primitive)
        {
            WriteHeader(CborMajorType.Primitive, (byte)primitive);
        }

        private void WriteHeader(CborMajorType majorType, byte additionalValue)
        {
            byte header = (byte)(((byte)majorType) << 5 | (additionalValue & 0x1f));
            WriteRawByte(header);
        }

        private void WriteRawByte(byte b)
        {
            Span<byte> buffer = _bufferWriter.GetSpan(1);
            buffer[0] = b;
            _bufferWriter.Advance(1);
        }
    }
}
