using Dahomey.Cbor.Util;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.ComponentModel;
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

        /// <summary>
        /// Default nesting limit, matching <see cref="CborOptions.MaxDepth"/> and
        /// <c>System.Text.Json</c>.
        /// </summary>
        public const int DefaultMaxDepth = 64;

        private IBufferWriter<byte> _bufferWriter;
        private readonly int _maxDepth;
        private int _depth;

        public IBufferWriter<byte> BufferWriter => _bufferWriter;

        /// <summary>Maximum permitted nesting depth of maps and arrays.</summary>
        public int MaxDepth => _maxDepth;

        private readonly bool _deterministic;

        public CborWriter(IBufferWriter<byte> bufferWriter)
            : this(bufferWriter, DefaultMaxDepth)
        {
        }

        /// <param name="maxDepth">
        /// Maximum nesting depth of maps and arrays. Exceeding it throws a <see cref="CborException"/>
        /// rather than recursing until the stack is exhausted, which is what an object graph
        /// containing a reference cycle would otherwise do. Must be positive.
        /// </param>
        public CborWriter(IBufferWriter<byte> bufferWriter, int maxDepth)
            : this(bufferWriter, maxDepth, deterministic: false)
        {
        }

        /// <param name="deterministic">
        /// Refuse indefinite lengths, which admit more than one encoding of the same value. Checked
        /// here rather than only on <see cref="CborOptions"/> because the options are the
        /// lowest-priority source of the length mode: <see cref="Attributes.CborLengthModeAttribute"/>
        /// on a type or member outranks them, and a converter may pass a mode explicitly. This is the
        /// one point every definite or indefinite header passes through.
        /// </param>
        public CborWriter(IBufferWriter<byte> bufferWriter, int maxDepth, bool deterministic)
        {
            if (maxDepth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "maxDepth must be positive.");
            }

            _bufferWriter = bufferWriter;
            _maxDepth = maxDepth;
            _depth = 0;
            _deterministic = deterministic;
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
            WriteUnsigned(value);
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
            if ((float)half == value)
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
            if ((float)half == value)
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
            if (value.IsEmpty)
            {
                WriteNull();
                return;
            }

            WriteInteger(CborMajorType.TextString, (ulong)value.Length);
            _bufferWriter.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value)
        {
            Span<byte> bytes = stackalloc byte[4];
            int len;
            unsafe
            {
                fixed (byte* rawBytes = bytes)
                {
                    len = Encoding.UTF8.GetBytes(&value, 1, rawBytes, 4);
                    ReadOnlySpan<byte> exactBytes = bytes.Slice(0, len);
                    WriteInteger(CborMajorType.TextString, (ulong)len);
                    _bufferWriter.Write(exactBytes);
                }
            }
        }

        /// <summary>
        /// Write the string header with a given size.
        /// This leaves the writer in an invalid state and must be accompanied with a write to <see cref="BufferWriter"/> with exactly <paramref name="size"/> bytes.
        /// </summary>
        /// <param name="size">The byte string size</param>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public void WriteStringSize(int size)
        {
            WriteSize(CborMajorType.TextString, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteString(ReadOnlySpan<byte> value)
        {
            WriteInteger(CborMajorType.ByteString, (ulong)value.Length);
            _bufferWriter.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteString(ReadOnlySequence<byte> value)
        {
            WriteInteger(CborMajorType.ByteString, (ulong)value.Length);
            foreach (var segment in value)
            {
                _bufferWriter.Write(segment.Span);
            }
        }

        /// <summary>
        /// Write the byte string header with a given size.
        /// This leaves the writer in an invalid state and must be accompanied with a write to <see cref="BufferWriter"/> with exactly <paramref name="size"/> bytes.
        /// </summary>
        /// <param name="size">The byte string size</param>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public void WriteByteStringSize(int size)
        {
            WriteSize(CborMajorType.ByteString, size);
        }

        public void WriteBeginMap(int size)
        {
            RejectIndefiniteWhenDeterministic(size, "map");
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
            EnterNestedItem();
            int size = mapWriter.GetMapSize(ref context);
            WriteBeginMap(size);
            while (mapWriter.WriteMapItem(ref this, ref context));
            WriteEndMap(size);
            _depth--;
        }

        public void WriteBeginArray(int size)
        {
            RejectIndefiniteWhenDeterministic(size, "array");
            WriteSize(CborMajorType.Array, size);
        }

        private readonly void RejectIndefiniteWhenDeterministic(int size, string kind)
        {
            if (_deterministic && size < 0)
            {
                throw new CborException(
                    $"An indefinite-length {kind} cannot be written while Deterministic is enabled: "
                    + "indefinite lengths admit more than one encoding of the same value. This can come "
                    + "from CborOptions.ArrayLengthMode or MapLengthMode, or from a CborLengthMode "
                    + "attribute on a type or member, which takes priority over both.");
            }
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
            EnterNestedItem();
            int size = arrayWriter.GetArraySize(ref context);
            WriteBeginArray(size);
            while(arrayWriter.WriteArrayItem(ref this, ref context));
            WriteEndArray(size);
            _depth--;
        }

        /// <summary>
        /// Accounts for entering a nested map or array, and refuses to go deeper than
        /// <see cref="MaxDepth"/>.
        /// </summary>
        /// <remarks>
        /// CBOR has no back-references, so a reference cycle in the object graph is not representable
        /// and manifests as unbounded recursion. Without this check that is a
        /// <c>StackOverflowException</c>, which cannot be caught and takes the process down; with it,
        /// the caller gets a <see cref="CborException"/> naming the likely cause.
        /// <para>
        /// The matching decrement in <c>WriteMap</c>/<c>WriteArray</c> is deliberately not wrapped in a
        /// <c>finally</c>: a writer that has thrown is never written to again — it is a single-use
        /// <c>ref struct</c> the caller discards — so the depth left behind is unobservable, while an
        /// exception handler would make these methods ineligible for JIT inlining on the hot path.
        /// </para>
        /// </remarks>
        private void EnterNestedItem()
        {
            if (++_depth > _maxDepth)
            {
                throw new CborException(
                    $"CBOR nesting depth exceeded the configured maximum of {_maxDepth}. " +
                    "This usually means the object graph contains a reference cycle, which CBOR cannot " +
                    $"represent. Raise {nameof(CborOptions)}.{nameof(CborOptions.MaxDepth)} if the data is " +
                    "genuinely this deeply nested.");
            }
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
            if (Half.IsNaN(value))
            {
                value = HalfHelpers.UInt16BitsToHalf(0x7e00);
            }

            WritePrimitive(CborPrimitive.HalfFloat);

            Span<byte> bytes = _bufferWriter.GetSpan(2);
            HalfHelpers.WriteHalf(bytes, value);
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
#if NET8_0_OR_GREATER
                MemoryMarshal.Write(bytes, in value);
#else
                MemoryMarshal.Write(bytes, ref value);
#endif
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
#if NET8_0_OR_GREATER
                MemoryMarshal.Write(bytes, in value);
#else
                MemoryMarshal.Write(bytes, ref value);
#endif
            }

            _bufferWriter.Advance(8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InternalWriteDecimal(decimal value)
        {
            WritePrimitive(CborPrimitive.DecimalFloat);

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
#if NET8_0_OR_GREATER
                MemoryMarshal.Write(span, in value);
#else
                MemoryMarshal.Write(span, ref value);
#endif
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
