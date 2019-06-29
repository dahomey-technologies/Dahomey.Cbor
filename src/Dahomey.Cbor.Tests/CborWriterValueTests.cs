using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborWriterValueTests
    {
        [DataTestMethod]
        [DataRow("F5", true, null)]
        [DataRow("F4", false, null)]
        public void WriteBoolean(string hexBuffer, bool value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("3B7FFFFFFFFFFFFFFF", long.MinValue, null)]
        [DataRow("3903E7", -1000L, null)]
        [DataRow("3863", -100L, null)]
        [DataRow("3818", -25L, null)]
        [DataRow("37", -24L, null)]
        [DataRow("2B", -12L, null)]
        [DataRow("1BFFFFFFFFFFFFFFFF", -1, typeof(CborException))]
        [DataRow("18", -1, typeof(CborException))]
        [DataRow("19", -1, typeof(CborException))]
        [DataRow("1A", -1, typeof(CborException))]
        [DataRow("1B", -1, typeof(CborException))]
        [DataRow("1C", -1, typeof(CborException))]
        [DataRow("1D", -1, typeof(CborException))]
        [DataRow("1E", -1, typeof(CborException))]
        [DataRow("1F", -1, typeof(CborException))]
        public void WriteNegative(string hexBuffer, long value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
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
        public void WritePositive(string hexBuffer, ulong value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FA4141EB85", 12.12f, null)]
        [DataRow("FAFFC00000", float.NaN, null)]
        [DataRow("FA7F800000", float.PositiveInfinity, null)]
        [DataRow("FAFF800000", float.NegativeInfinity, null)]
        public void WriteSingle(string hexBuffer, float value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FB40283D70A3D70A3D", 12.12, null)]
        [DataRow("FBFFF8000000000000", double.NaN, null)]
        [DataRow("FB7FF0000000000000", double.PositiveInfinity, null)]
        [DataRow("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void WriteDouble(string hexBuffer, double value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("63666F6F", "foo", null)]
        [DataRow("60", "", null)]
        public void WriteString(string hexBuffer, string value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [TestMethod]
        public void WriteNull()
        {
            TestWrite(CborValue.Null, "F6");
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void WriteInt32List(string hexBuffer, string value, Type expectedExceptionType)
        {
            CborArray array = new CborArray(value.Split(',').Select(s => (CborValue)int.Parse(s)));
            TestWrite(array, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void WriteStringList(string hexBuffer, string value, Type expectedExceptionType)
        {
            CborArray array = new CborArray(value.Split(',').Select(s => (CborValue)s));
            TestWrite(array, hexBuffer, expectedExceptionType);
        }

        [TestMethod]
        public void WriteObject()
        {
            const string hexBuffer =
                "A666737472696E6763666F6F666E756D626572FB40283D70A3D70A3D64626F6F6CF5646E756C6CF6656172726179820102666F626A656374A162696401";
            CborValue obj = new Dictionary<string, CborValue>
            {
                {"string", "foo"},
                {"number", 12.12},
                {"bool", true},
                {"null", null},
                {"array", new CborValue[] {1, 2}},
                {"object", new CborPair("id", 1)},
            };
            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteArray()
        {
            string hexBuffer = "8663666F6FFB40283D70A3D70A3DF5F6820102A162696401";
            CborValue array = new CborValue[]
            {
                "foo",
                12.12,
                true,
                null,
                new CborValue[] {1, 2},
                new CborPair("id", 1)
            };
            TestWrite(array, hexBuffer);
        }

        private string Write(CborValue value, CborSerializationSettings settings = null)
        {
            ByteBufferWriter bufferWriter = new ByteBufferWriter();
            CborWriter writer = new CborWriter(bufferWriter, settings);
            ICborConverter<CborValue> converter = CborConverter.Lookup<CborValue>();
            converter.Write(ref writer, value);
            return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
        }

        private void TestWrite(CborValue value, string hexBuffer, Type expectedExceptionType = null, CborSerializationSettings settings = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Write(value, settings);
                }
                catch (Exception ex)
                {
                    Assert.IsInstanceOfType(ex, expectedExceptionType);
                }
            }
            else
            {
                Assert.AreEqual(hexBuffer, Write(value, settings));
            }
        }
    }
}
