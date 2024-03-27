using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using Xunit;
using System;
using System.Globalization;
using System.Buffers;
using System.Text;

namespace Dahomey.Cbor.Tests
{
    public class CborWriterTests
    {
        static CborWriterTests()
        {
            SampleClasses.Initialize();
        }

        [Theory]
        [InlineData("F5", true, null)]
        [InlineData("F4", false, null)]
        public void WriteBoolean(string hexBuffer, bool value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteBoolean), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("187F", sbyte.MaxValue, null)]
        [InlineData("387F", sbyte.MinValue, null)]
        [InlineData("0C", (sbyte)12, null)]
        [InlineData("2B", (sbyte)-12, null)]
        [InlineData("00", (sbyte)0, null)]
        [InlineData("61", (sbyte)0, typeof(CborException))]
        public void WriteSByte(string hexBuffer, sbyte value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteSByte), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("18FF", byte.MaxValue, null)]
        [InlineData("0C", (byte)12, null)]
        [InlineData("2B", (byte)0, typeof(CborException))]
        [InlineData("00", (byte)0, null)]
        [InlineData("61", (byte)0, typeof(CborException))]
        public void WriteByte(string hexBuffer, byte value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteByte), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("197FFF", short.MaxValue, null)]
        [InlineData("397FFF", short.MinValue, null)]
        [InlineData("0C", (short)12, null)]
        [InlineData("2B", (short)-12, null)]
        [InlineData("00", (short)0, null)]
        [InlineData("61", (short)0, typeof(CborException))]
        public void WriteInt16(string hexBuffer, short value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteInt16), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("19FFFF", ushort.MaxValue, null)]
        [InlineData("0C", (ushort)12, null)]
        [InlineData("2B", (ushort)0, typeof(CborException))]
        [InlineData("00", (ushort)0, null)]
        [InlineData("61", (ushort)0, typeof(CborException))]
        public void WriteUInt16(string hexBuffer, ushort value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteUInt16), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("1A7FFFFFFF", int.MaxValue, null)]
        [InlineData("3A7FFFFFFF", int.MinValue, null)]
        [InlineData("0C", 12, null)]
        [InlineData("2B", -12, null)]
        [InlineData("00", 0, null)]
        [InlineData("61", 0, typeof(CborException))]
        public void WriteInt32(string hexBuffer, int value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteInt32), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("1AFFFFFFFF", uint.MaxValue, null)]
        [InlineData("0C", 12u, null)]
        [InlineData("2B", 0u, typeof(CborException))]
        [InlineData("00", 0u, null)]
        [InlineData("61", 0u, typeof(CborException))]
        public void WriteUInt32(string hexBuffer, uint value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteUInt32), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("3B7FFFFFFFFFFFFFFF", long.MinValue, null)]
        [InlineData("3903E7", -1000L, null)]
        [InlineData("3863", -100L, null)]
        [InlineData("3818", -25L, null)]
        [InlineData("37", -24L, null)]
        [InlineData("2B", -12L, null)]
        [InlineData("00", 0L, null)]
        [InlineData("0C", 12L, null)]
        [InlineData("17", 23L, null)]
        [InlineData("1818", 24L, null)]
        [InlineData("1864", 100L, null)]
        [InlineData("1903E8", 1000L, null)]
        [InlineData("1B7FFFFFFFFFFFFFFF", long.MaxValue, null)]
        [InlineData("1BFFFFFFFFFFFFFFFF", 0, typeof(CborException))]
        [InlineData("18", 0, typeof(CborException))]
        [InlineData("19", 0, typeof(CborException))]
        [InlineData("1A", 0, typeof(CborException))]
        [InlineData("1B", 0, typeof(CborException))]
        [InlineData("1C", 0, typeof(CborException))]
        [InlineData("1D", 0, typeof(CborException))]
        [InlineData("1E", 0, typeof(CborException))]
        [InlineData("1F", 0, typeof(CborException))]
        public void WriteInt64(string hexBuffer, long value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteInt64), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("3B7FFFFFFFFFFFFFFF", 0ul, typeof(CborException))]
        [InlineData("3903E7", 0ul, typeof(CborException))]
        [InlineData("3863", 0ul, typeof(CborException))]
        [InlineData("3818", 0ul, typeof(CborException))]
        [InlineData("37", 0ul, typeof(CborException))]
        [InlineData("2B", 0ul, typeof(CborException))]
        [InlineData("00", 0ul, null)]
        [InlineData("0C", 12ul, null)]
        [InlineData("17", 23ul, null)]
        [InlineData("1818", 24ul, null)]
        [InlineData("1864", 100ul, null)]
        [InlineData("1903E8", 1000ul, null)]
        [InlineData("1B7FFFFFFFFFFFFFFF", (ulong)long.MaxValue, null)]
        [InlineData("1BFFFFFFFFFFFFFFFF", ulong.MaxValue, null)]
        [InlineData("18", 0ul, typeof(CborException))]
        [InlineData("19", 0ul, typeof(CborException))]
        [InlineData("1A", 0ul, typeof(CborException))]
        [InlineData("1B", 0ul, typeof(CborException))]
        [InlineData("1C", 0ul, typeof(CborException))]
        [InlineData("1D", 0ul, typeof(CborException))]
        [InlineData("1E", 0ul, typeof(CborException))]
        [InlineData("1F", 0ul, typeof(CborException))]
        public void WriteUInt64(string hexBuffer, ulong value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteUInt64), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("F9E3D0", -1000f, null)]
        [InlineData("F90000", 0f, null)]
        [InlineData("F98000", -0f, null)]
        [InlineData("F93C00", 1f, null)]
        [InlineData("F93E00", 1.5f, null)]
        [InlineData("F97BFF", 65504f, null)]
        [InlineData("F90001", 5.960464477539063e-8f, null)]
        [InlineData("F90400", 0.00006103515625f, null)]
        [InlineData("F9C400", -4f, null)]
        [InlineData("F97E00", float.NaN, null)]
        [InlineData("F97C00", float.PositiveInfinity, null)]
        [InlineData("F9FC00", float.NegativeInfinity, null)]
        public void WriteHalf(string hexBuffer, float value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteHalf), (Half)value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        // half
        [InlineData("F9E3D0", -1000f, null)]
        [InlineData("F90000", 0f, null)]
        [InlineData("F98000", -0f, null)]
        [InlineData("F93C00", 1f, null)]
        [InlineData("F93E00", 1.5f, null)]
        [InlineData("F97BFF", 65504f, null)]
        [InlineData("F90001", 5.960464477539063e-8f, null)]
        [InlineData("F90400", 0.00006103515625f, null)]
        [InlineData("F9C400", -4f, null)]
        [InlineData("F97E00", float.NaN, null)]
        [InlineData("F97C00", float.PositiveInfinity, null)]
        [InlineData("F9FC00", float.NegativeInfinity, null)]
        // single
        [InlineData("FA4141EB85", 12.12f, null)]
        public void WriteSingle(string hexBuffer, float value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteSingle), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        // half
        [InlineData("F9E3D0", -1000.0, null)]
        [InlineData("F90000", 0.0, null)]
        [InlineData("F98000", -0.0, null)]
        [InlineData("F93C00", 1.0, null)]
        [InlineData("F93E00", 1.5, null)]
        [InlineData("F97BFF", 65504.0, null)]
        [InlineData("F90001", 5.960464477539063e-8, null)]
        [InlineData("F90400", 0.00006103515625, null)]
        [InlineData("F9C400", -4.0, null)]
        [InlineData("F97E00", double.NaN, null)]
        [InlineData("F97C00", double.PositiveInfinity, null)]
        [InlineData("F9FC00", double.NegativeInfinity, null)]
        // single
        [InlineData("FA4141EB85", 12.119999885559082, null)]
        [InlineData("FA47C35000", 100000.0, null)]
        // double
        [InlineData("FB40283D70A3D70A3D", 12.12, null)]
        public void WriteDouble(string hexBuffer, double value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteDouble), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("FC00013D2B9C2D125A0000000000080000", 3487324.89798234, null)]
        public void WriteDecimal(string hexBuffer, decimal value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteDecimal), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("FC0DED017037E8D1950000000000100000", "100.3459873459458453", null)]
        [InlineData("FCFFFFFFFFFFFFFFFFFFFFFFFF80000000", "-79228162514264337593543950335", null)]
        [InlineData("FCFFFFFFFFFFFFFFFFFFFFFFFF00000000", "79228162514264337593543950335", null)]
        public void WriteDecimal2(string hexBuffer, string value, Type expectedExceptionType)
        {
            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decimalValue)) throw new InvalidCastException("Specified string cannot be cast to a decimal value");

            Helper.TestWrite(nameof(CborWriter.WriteDecimal), decimalValue, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("63666F6F", "foo", null)]
        [InlineData("F6", null, null)]
        [InlineData("60", "", null)]
        [InlineData("68C3A9C3A0C3AAC3AF", "éàêï", null)]
        public void WriteString(string hexBuffer, string value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteString), value, hexBuffer, expectedExceptionType);
        }

        [Fact]
        public void WriteStringSize()
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            var cborWriter = new CborWriter(bufferWriter);
            cborWriter.WriteString("Hello world");
            var expected = bufferWriter.WrittenSpan.ToArray();

            bufferWriter.Clear();

            cborWriter.WriteStringSize(11);
            cborWriter.BufferWriter.Write(Encoding.UTF8.GetBytes("Hello world"));
            var actual = bufferWriter.WrittenSpan.ToArray();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WriteByteString(bool fragmentize)
        {
            var bytes = new byte[] { 0xde, 0xad, 0xbe, 0xef };
            var bufferWriter = new ArrayBufferWriter<byte>();
            var cborWriter = new CborWriter(bufferWriter);
            cborWriter.WriteByteString(bytes.AsSpan());
            var expected = bufferWriter.WrittenSpan.ToArray();

            bufferWriter.Clear();

            var value = fragmentize
                ? Helper.Fragmentize(bytes)
                : new ReadOnlySequence<byte>(bytes);

            cborWriter.WriteByteString(value);
            var actual = bufferWriter.WrittenSpan.ToArray();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void WriteByteStringSize()
        {
            var bufferWriter = new ArrayBufferWriter<byte>();
            var cborWriter = new CborWriter(bufferWriter);
            cborWriter.WriteByteString(new byte[] { 0xde, 0xad, 0xbe, 0xef });
            var expected = bufferWriter.WrittenSpan.ToArray();

            bufferWriter.Clear();

            cborWriter.WriteByteStringSize(4);
            cborWriter.BufferWriter.Write(new byte[] { 0xde, 0xad, 0xbe, 0xef });
            var actual = bufferWriter.WrittenSpan.ToArray();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("1A5D2A3DDB", ulong.MaxValue)]
        [InlineData("C01A5D2A3DDB", 0ul)]
        [InlineData("D8641A5D2A3DDB", 100ul)]
        public void WriteSemanticTag(string hexBuffer, ulong semanticTag)
        {
            using (ByteBufferWriter buffer = new ByteBufferWriter())
            {
                CborWriter writer = new CborWriter(buffer);

                if (semanticTag != ulong.MaxValue)
                {
                    writer.WriteSemanticTag(semanticTag);
                }
                writer.WriteUInt32(1563049435);

                Assert.Equal(hexBuffer, BitConverter.ToString(buffer.WrittenSpan.ToArray()).Replace("-", ""));
            }
        }

        [Theory]
        [InlineData(42, "182A")]
        [InlineData("Hello world", "6B48656C6C6F20776F726C64")]
        [InlineData(false, "F4")]
        public void WriteDataTypeCastAsObject(object value, string hexBuffer)
        {
            Helper.TestWrite(value, hexBuffer);
        }
    }
}
