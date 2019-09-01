using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using Xunit;
using System;

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
        [InlineData("FA4141EB85", 12.12f, null)]
        [InlineData("FAFFC00000", float.NaN, null)]
        [InlineData("FA7F800000", float.PositiveInfinity, null)]
        [InlineData("FAFF800000", float.NegativeInfinity, null)]
        public void WriteSingle(string hexBuffer, float value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteSingle), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("FB40283D70A3D70A3D", 12.12, null)]
        [InlineData("FBFFF8000000000000", double.NaN, null)]
        [InlineData("FB7FF0000000000000", double.PositiveInfinity, null)]
        [InlineData("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void WriteDouble(string hexBuffer, double value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteDouble), value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("63666F6F", "foo", null)]
        [InlineData("F6", null, null)]
        [InlineData("60", "", null)]
        public void WriteString(string hexBuffer, string value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteString), value, hexBuffer, expectedExceptionType);
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
    }
}
