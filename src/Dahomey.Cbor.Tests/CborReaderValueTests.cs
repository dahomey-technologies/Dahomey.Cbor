using Dahomey.Cbor.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborReaderValueTests
    {
        [DataTestMethod]
        [DataRow("F5", true, null)]
        [DataRow("F4", false, null)]
        public void ReadBoolean(string hexBuffer, bool value, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)value, expectedExceptionType);
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
        public void ReadNegative(string hexBuffer, long expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
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
        public void ReadPositive(string hexBuffer, ulong expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FA4141EB85", 12.12f, null)]
        [DataRow("FAFFC00000", float.NaN, null)]
        [DataRow("FA7F800000", float.PositiveInfinity, null)]
        [DataRow("FAFF800000", float.NegativeInfinity, null)]
        public void ReadSingle(string hexBuffer, float expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FB40283D70A3D70A3D", 12.12, null)]
        [DataRow("FBFFF8000000000000", double.NaN, null)]
        [DataRow("FB7FF0000000000000", double.PositiveInfinity, null)]
        [DataRow("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void ReadDouble(string hexBuffer, double expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("63666F6F", "foo", null)]
        [DataRow("60", "", null)]
        public void ReadString(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [TestMethod]
        public void ReadNull()
        {
            Helper.TestRead("F6", (CborValue)CborValue.Null);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void ReadInt32List(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            CborArray array = new CborArray(expectedValue.Split(',').Select(s => (CborValue)int.Parse(s)));
            Helper.TestRead(hexBuffer, array, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void ReadStringList(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            CborArray array = new CborArray(expectedValue.Split(',').Select(s => (CborValue)s));
            Helper.TestRead(hexBuffer, array, expectedExceptionType);
        }

        [TestMethod]
        public void ReadObject()
        {
            const string hexBuffer =
                "A666737472696E6763666F6F666E756D626572FB40283D70A3D70A3D64626F6F6CF5646E756C6CF6656172726179820102666F626A656374A162696401";
            CborObject actualObject = Helper.Read<CborObject>(hexBuffer);
            Assert.IsNotNull(actualObject);

            // pairs
            Assert.AreEqual(6, actualObject.Count);

            // string
            Assert.IsTrue(actualObject.TryGetValue("string", out CborValue value));
            Assert.AreEqual(CborValueType.String, value.Type);
            Assert.IsInstanceOfType(value, typeof(CborString));
            Assert.AreEqual("foo", value.Value<string>());

            // number
            Assert.IsTrue(actualObject.TryGetValue("number", out value));
            Assert.AreEqual(CborValueType.Double, value.Type);
            Assert.IsInstanceOfType(value, typeof(CborDouble));
            Assert.AreEqual(12.12, value.Value<double>(), 3);

            // bool
            Assert.IsTrue(actualObject.TryGetValue("bool", out value));
            Assert.AreEqual(CborValueType.Boolean, value.Type);
            Assert.IsInstanceOfType(value, typeof(CborBoolean));
            Assert.IsTrue(value.Value<bool>());

            // null
            Assert.IsTrue(actualObject.TryGetValue("null", out value));
            Assert.AreEqual(CborValueType.Null, value.Type);
            Assert.IsInstanceOfType(value, typeof(CborNull));

            // array
            Assert.IsTrue(actualObject.TryGetValue("array", out value));
            Assert.AreEqual(CborValueType.Array, value.Type);
            Assert.IsInstanceOfType(value, typeof(CborArray));
            CborArray CborArray = (CborArray)value;
            Assert.AreEqual(2, CborArray.Count);
            Assert.AreEqual(1, CborArray[0].Value<double>());
            Assert.AreEqual(2, CborArray[1].Value<double>());

            // object
            Assert.IsTrue(actualObject.TryGetValue("object", out value));
            Assert.AreEqual(CborValueType.Object, value.Type);
            Assert.IsInstanceOfType(value, typeof(CborObject));
            CborObject cborObject = (CborObject)value;
            Assert.IsTrue(cborObject.TryGetValue("id", out value));
            Assert.AreEqual(CborValueType.Positive, value.Type);
            Assert.AreEqual(1, value.Value<int>());
        }

        [TestMethod]
        public void ReadArray()
        {
            string hexBuffer = "8663666F6FFB40283D70A3D70A3DF5F6820102A162696401";
            CborArray actualArray = Helper.Read<CborArray>(hexBuffer);
            Assert.IsNotNull(actualArray);

            // values
            Assert.AreEqual(6, actualArray.Count);

            // string
            CborValue actualString = actualArray[0];
            Assert.IsNotNull(actualString);
            Assert.AreEqual(CborValueType.String, actualString.Type);
            Assert.IsInstanceOfType(actualString, typeof(CborString));
            Assert.AreEqual("foo", actualString.Value<string>());

            // number
            CborValue actualNumber = actualArray[1];
            Assert.IsNotNull(actualNumber);
            Assert.AreEqual(CborValueType.Double, actualNumber.Type);
            Assert.IsInstanceOfType(actualNumber, typeof(CborDouble));
            Assert.AreEqual(12.12, actualNumber.Value<double>(), 3);

            // bool
            CborValue actualBool = actualArray[2];
            Assert.IsNotNull(actualBool);
            Assert.AreEqual(CborValueType.Boolean, actualBool.Type);
            Assert.IsInstanceOfType(actualBool, typeof(CborBoolean));
            Assert.IsTrue(actualBool.Value<bool>());

            // null
            CborValue actualNull = actualArray[3];
            Assert.IsNotNull(actualNull);
            Assert.AreEqual(CborValueType.Null, actualNull.Type);

            // array
            CborValue actualArrayValue = actualArray[4];
            Assert.IsNotNull(actualArrayValue);
            Assert.AreEqual(CborValueType.Array, actualArrayValue.Type);
            Assert.IsInstanceOfType(actualArrayValue, typeof(CborArray));
            CborArray CborArray = (CborArray)actualArrayValue;
            Assert.AreEqual(2, CborArray.Count);
            Assert.AreEqual(1, CborArray[0].Value<double>());
            Assert.AreEqual(2, CborArray[1].Value<double>());

            // object
            CborValue actualObject = actualArray[5];
            Assert.IsNotNull(actualObject);
            Assert.AreEqual(CborValueType.Object, actualObject.Type);
            Assert.IsInstanceOfType(actualObject, typeof(CborObject));
            CborObject cborObject = (CborObject)actualObject;
            Assert.IsTrue(cborObject.TryGetValue("id", out CborValue value));
            Assert.AreEqual(CborValueType.Positive, value.Type);
            Assert.AreEqual(1, value.Value<int>());
        }
    }
}
