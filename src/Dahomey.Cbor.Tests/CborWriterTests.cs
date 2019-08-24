using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborWriterTests
    {
        static CborWriterTests()
        {
            SampleClasses.Initialize();
        }

        [DataTestMethod]
        [DataRow("F5", true, null)]
        [DataRow("F4", false, null)]
        public void WriteBoolean(string hexBuffer, bool value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteBoolean), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("187F", sbyte.MaxValue, null)]
        [DataRow("387F", sbyte.MinValue, null)]
        [DataRow("0C", (sbyte)12, null)]
        [DataRow("2B", (sbyte)-12, null)]
        [DataRow("00", (sbyte)0, null)]
        [DataRow("61", (sbyte)0, typeof(CborException))]
        public void WriteSByte(string hexBuffer, sbyte value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteSByte), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("18FF", byte.MaxValue, null)]
        [DataRow("0C", (byte)12, null)]
        [DataRow("2B", (byte)0, typeof(CborException))]
        [DataRow("00", (byte)0, null)]
        [DataRow("61", (byte)0, typeof(CborException))]
        public void WriteByte(string hexBuffer, byte value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteByte), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("197FFF", short.MaxValue, null)]
        [DataRow("397FFF", short.MinValue, null)]
        [DataRow("0C", (short)12, null)]
        [DataRow("2B", (short)-12, null)]
        [DataRow("00", (short)0, null)]
        [DataRow("61", (short)0, typeof(CborException))]
        public void WriteInt16(string hexBuffer, short value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteInt16), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("19FFFF", ushort.MaxValue, null)]
        [DataRow("0C", (ushort)12, null)]
        [DataRow("2B", (ushort)0, typeof(CborException))]
        [DataRow("00", (ushort)0, null)]
        [DataRow("61", (ushort)0, typeof(CborException))]
        public void WriteUInt16(string hexBuffer, ushort value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteUInt16), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1A7FFFFFFF", int.MaxValue, null)]
        [DataRow("3A7FFFFFFF", int.MinValue, null)]
        [DataRow("0C", 12, null)]
        [DataRow("2B", -12, null)]
        [DataRow("00", 0, null)]
        [DataRow("61", 0, typeof(CborException))]
        public void WriteInt32(string hexBuffer, int value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteInt32), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1AFFFFFFFF", uint.MaxValue, null)]
        [DataRow("0C", 12u, null)]
        [DataRow("2B", 0u, typeof(CborException))]
        [DataRow("00", 0u, null)]
        [DataRow("61", 0u, typeof(CborException))]
        public void WriteUInt32(string hexBuffer, uint value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteUInt32), value, hexBuffer, expectedExceptionType);
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
        public void WriteInt64(string hexBuffer, long value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteInt64), value, hexBuffer, expectedExceptionType);
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
        public void WriteUInt64(string hexBuffer, ulong value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteUInt64), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FA4141EB85", 12.12f, null)]
        [DataRow("FAFFC00000", float.NaN, null)]
        [DataRow("FA7F800000", float.PositiveInfinity, null)]
        [DataRow("FAFF800000", float.NegativeInfinity, null)]
        public void WriteSingle(string hexBuffer, float value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteSingle), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FB40283D70A3D70A3D", 12.12, null)]
        [DataRow("FBFFF8000000000000", double.NaN, null)]
        [DataRow("FB7FF0000000000000", double.PositiveInfinity, null)]
        [DataRow("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void WriteDouble(string hexBuffer, double value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteDouble), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("63666F6F", "foo", null)]
        [DataRow("F6", null, null)]
        [DataRow("60", "", null)]
        public void WriteString(string hexBuffer, string value, Type expectedExceptionType)
        {
            Helper.TestWrite(nameof(CborWriter.WriteString), value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1A5D2A3DDB", ulong.MaxValue)]
        [DataRow("C01A5D2A3DDB", 0ul)]
        [DataRow("D8641A5D2A3DDB", 100ul)]
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

                Assert.AreEqual(hexBuffer, BitConverter.ToString(buffer.WrittenSpan.ToArray()).Replace("-", ""));
            }
        }
    }
}
