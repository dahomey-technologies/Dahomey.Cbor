using Dahomey.Cbor.Serialization;
using Xunit;
using System;

namespace Dahomey.Cbor.Tests
{

    public class CborReaderTests
    {
        [Theory]
        [InlineData("F5", true, null)]
        [InlineData("F4", false, null)]
        public void ReadBoolean(string hexBuffer, bool expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadBoolean), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("187F", sbyte.MaxValue, null)]
        [InlineData("387F", sbyte.MinValue, null)]
        [InlineData("0C", (sbyte)12, null)]
        [InlineData("2B", (sbyte)-12, null)]
        [InlineData("00", (sbyte)0, null)]
        [InlineData("61", (sbyte)0, typeof(CborException))]
        public void ReadSByte(string hexBuffer, sbyte expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadSByte), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("18FF", byte.MaxValue, null)]
        [InlineData("0C", (byte)12, null)]
        [InlineData("2B", (byte)0, typeof(CborException))]
        [InlineData("00", (byte)0, null)]
        [InlineData("61", (byte)0, typeof(CborException))]
        public void ReadByte(string hexBuffer, byte expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadByte), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("197FFF", short.MaxValue, null)]
        [InlineData("397FFF", short.MinValue, null)]
        [InlineData("0C", (short)12, null)]
        [InlineData("2B", (short)-12, null)]
        [InlineData("00", (short)0, null)]
        [InlineData("61", (short)0, typeof(CborException))]
        public void ReadInt16(string hexBuffer, short expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadInt16), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("19FFFF", ushort.MaxValue, null)]
        [InlineData("0C", (ushort)12, null)]
        [InlineData("2B", (ushort)0, typeof(CborException))]
        [InlineData("00", (ushort)0, null)]
        [InlineData("61", (ushort)0, typeof(CborException))]
        public void ReadUInt16(string hexBuffer, ushort expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadUInt16), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("1A7FFFFFFF", int.MaxValue, null)]
        [InlineData("3A7FFFFFFF", int.MinValue, null)]
        [InlineData("0C", 12, null)]
        [InlineData("2B", -12, null)]
        [InlineData("00", 0, null)]
        [InlineData("61", 0, typeof(CborException))]
        public void ReadInt32(string hexBuffer, int expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadInt32), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("1AFFFFFFFF", uint.MaxValue, null)]
        [InlineData("0C", 12u, null)]
        [InlineData("2B", 0u, typeof(CborException))]
        [InlineData("00", 0u, null)]
        [InlineData("61", 0u, typeof(CborException))]
        public void ReadUInt32(string hexBuffer, uint expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadUInt32), hexBuffer, expectedValue, expectedExceptionType);
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
        public void ReadInt64(string hexBuffer, long expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadInt64), hexBuffer, expectedValue, expectedExceptionType);
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
        public void ReadUInt64(string hexBuffer, ulong expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadUInt64), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("FA4141EB85", 12.12f, null)]
        [InlineData("FAFFC00000", float.NaN, null)]
        [InlineData("FA7F800000", float.PositiveInfinity, null)]
        [InlineData("FAFF800000", float.NegativeInfinity, null)]
        public void ReadSingle(string hexBuffer, float expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadSingle), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("FB40283D70A3D70A3D", 12.12, null)]
        [InlineData("FBFFF8000000000000", double.NaN, null)]
        [InlineData("FB7FF0000000000000", double.PositiveInfinity, null)]
        [InlineData("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void ReadDouble(string hexBuffer, double expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadDouble), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("63666F6F", "foo", null)]
        [InlineData("7F6166616F616FFF", "foo", typeof(NotSupportedException))]
        [InlineData("F6", null, null)]
        [InlineData("60", "", null)]
        [InlineData("68C3A9C3A0C3AAC3AF", "éàêï", null)]
        public void ReadString(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadString), hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("1A5D2A3DDB", ulong.MaxValue)]
        [InlineData("C01A5D2A3DDB", 0ul)]
        [InlineData("D8641A5D2A3DDB", 100ul)]
        public void ReadSemanticTag(string hexBuffer, ulong expectedTag)
        {
            ReadOnlySpan<byte> buffer = hexBuffer.HexToBytes();
            CborReader reader = new CborReader(buffer);
            bool hasTag = reader.TryReadSemanticTag(out ulong actualTag);

            Assert.Equal(expectedTag != ulong.MaxValue, hasTag);
            Assert.Equal(expectedTag != ulong.MaxValue ? expectedTag : 0, actualTag);
        }
    }
}
