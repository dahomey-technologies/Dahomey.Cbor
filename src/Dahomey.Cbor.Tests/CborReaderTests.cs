using Dahomey.Cbor.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborReaderTests
    {
        [DataTestMethod]
        [DataRow("F5", true, null)]
        [DataRow("F4", false, null)]
        public void ReadBoolean(string hexBuffer, bool expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadBoolean), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("187F", sbyte.MaxValue, null)]
        [DataRow("387F", sbyte.MinValue, null)]
        [DataRow("0C", (sbyte)12, null)]
        [DataRow("2B", (sbyte)-12, null)]
        [DataRow("00", (sbyte)0, null)]
        [DataRow("61", (sbyte)0, typeof(CborException))]
        public void ReadSByte(string hexBuffer, sbyte expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadSByte), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("18FF", byte.MaxValue, null)]
        [DataRow("0C", (byte)12, null)]
        [DataRow("2B", (byte)0, typeof(CborException))]
        [DataRow("00", (byte)0, null)]
        [DataRow("61", (byte)0, typeof(CborException))]
        public void ReadByte(string hexBuffer, byte expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadByte), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("197FFF", short.MaxValue, null)]
        [DataRow("397FFF", short.MinValue, null)]
        [DataRow("0C", (short)12, null)]
        [DataRow("2B", (short)-12, null)]
        [DataRow("00", (short)0, null)]
        [DataRow("61", (short)0, typeof(CborException))]
        public void ReadInt16(string hexBuffer, short expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadInt16), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("19FFFF", ushort.MaxValue, null)]
        [DataRow("0C", (ushort)12, null)]
        [DataRow("2B", (ushort)0, typeof(CborException))]
        [DataRow("00", (ushort)0, null)]
        [DataRow("61", (ushort)0, typeof(CborException))]
        public void ReadUInt16(string hexBuffer, ushort expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadUInt16), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1A7FFFFFFF", int.MaxValue, null)]
        [DataRow("3A7FFFFFFF", int.MinValue, null)]
        [DataRow("0C", 12, null)]
        [DataRow("2B", -12, null)]
        [DataRow("00", 0, null)]
        [DataRow("61", 0, typeof(CborException))]
        public void ReadInt32(string hexBuffer, int expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadInt32), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1AFFFFFFFF", uint.MaxValue, null)]
        [DataRow("0C", 12u, null)]
        [DataRow("2B", 0u, typeof(CborException))]
        [DataRow("00", 0u, null)]
        [DataRow("61", 0u, typeof(CborException))]
        public void ReadUInt32(string hexBuffer, uint expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadUInt32), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("3B7FFFFFFFFFFFFFFF", long.MinValue, null)]
        [DataRow("3903E7", -1000L, null)]
        [DataRow("3863", -100L, null)]
        [DataRow("3818", -25L, null)]
        [DataRow("37", -24L, null)]
        [DataRow("2B", -12L, null)]
        [DataRow("00", 0L, null)]
        [DataRow("0C", 12L, null)]
        [DataRow("17", 23L, null)]
        [DataRow("1818", 24L, null)]
        [DataRow("1864", 100L, null)]
        [DataRow("1903E8", 1000L, null)]
        [DataRow("1B7FFFFFFFFFFFFFFF", long.MaxValue, null)]
        [DataRow("1BFFFFFFFFFFFFFFFF", 0, typeof(CborException))]
        [DataRow("18", 0, typeof(CborException))]
        [DataRow("19", 0, typeof(CborException))]
        [DataRow("1A", 0, typeof(CborException))]
        [DataRow("1B", 0, typeof(CborException))]
        [DataRow("1C", 0, typeof(CborException))]
        [DataRow("1D", 0, typeof(CborException))]
        [DataRow("1E", 0, typeof(CborException))]
        [DataRow("1F", 0, typeof(CborException))]
        public void ReadInt64(string hexBuffer, long expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadInt64), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("3B7FFFFFFFFFFFFFFF", 0ul, typeof(CborException))]
        [DataRow("3903E7", 0ul, typeof(CborException))]
        [DataRow("3863", 0ul, typeof(CborException))]
        [DataRow("3818", 0ul, typeof(CborException))]
        [DataRow("37", 0ul, typeof(CborException))]
        [DataRow("2B", 0ul, typeof(CborException))]
        [DataRow("00", 0ul, null)]
        [DataRow("0C", 12ul, null)]
        [DataRow("17", 23ul, null)]
        [DataRow("1818", 24ul, null)]
        [DataRow("1864", 100ul, null)]
        [DataRow("1903E8", 1000ul, null)]
        [DataRow("1B7FFFFFFFFFFFFFFF", (ulong)long.MaxValue, null)]
        [DataRow("1BFFFFFFFFFFFFFFFF", ulong.MaxValue, null)]
        [DataRow("18", 0ul, typeof(CborException))]
        [DataRow("19", 0ul, typeof(CborException))]
        [DataRow("1A", 0ul, typeof(CborException))]
        [DataRow("1B", 0ul, typeof(CborException))]
        [DataRow("1C", 0ul, typeof(CborException))]
        [DataRow("1D", 0ul, typeof(CborException))]
        [DataRow("1E", 0ul, typeof(CborException))]
        [DataRow("1F", 0ul, typeof(CborException))]
        public void ReadUInt64(string hexBuffer, ulong expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadUInt64), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FA4141EB85", 12.12f, null)]
        [DataRow("FAFFC00000", float.NaN, null)]
        [DataRow("FA7F800000", float.PositiveInfinity, null)]
        [DataRow("FAFF800000", float.NegativeInfinity, null)]
        public void ReadSingle(string hexBuffer, float expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadSingle), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FB40283D70A3D70A3D", 12.12, null)]
        [DataRow("FBFFF8000000000000", double.NaN, null)]
        [DataRow("FB7FF0000000000000", double.PositiveInfinity, null)]
        [DataRow("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void ReadDouble(string hexBuffer, double expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadDouble), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("63666F6F", "foo", null)]
        [DataRow("7F6166616F616FFF", "foo", typeof(NotSupportedException))]
        [DataRow("F6", null, null)]
        [DataRow("60", "", null)]
        public void ReadString(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(nameof(CborReader.ReadString), hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1A5D2A3DDB", ulong.MaxValue)]
        [DataRow("C01A5D2A3DDB", 0ul)]
        [DataRow("D8641A5D2A3DDB", 100ul)]
        public void ReadSemanticTag(string hexBuffer, ulong expectedTag)
        {
            ReadOnlySpan<byte> buffer = hexBuffer.HexToBytes();
            CborReader reader = new CborReader(buffer);
            bool hasTag = reader.TryReadSemanticTag(out ulong actualTag);

            Assert.AreEqual(expectedTag != ulong.MaxValue, hasTag);
            Assert.AreEqual(expectedTag != ulong.MaxValue ? expectedTag : 0, actualTag);
        }
    }
}
