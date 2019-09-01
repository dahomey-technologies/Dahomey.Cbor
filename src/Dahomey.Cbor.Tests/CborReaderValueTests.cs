using Dahomey.Cbor.ObjectModel;
using Xunit;
using System;
using System.Linq;

namespace Dahomey.Cbor.Tests
{

    public class CborReaderValueTests
    {
        [Theory]
        [InlineData("F5", true, null)]
        [InlineData("F4", false, null)]
        public void ReadBoolean(string hexBuffer, bool value, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)value, expectedExceptionType);
        }

        [Theory]
        [InlineData("3B7FFFFFFFFFFFFFFF", long.MinValue, null)]
        [InlineData("3903E7", -1000L, null)]
        [InlineData("3863", -100L, null)]
        [InlineData("3818", -25L, null)]
        [InlineData("37", -24L, null)]
        [InlineData("2B", -12L, null)]
        [InlineData("1BFFFFFFFFFFFFFFFF", -1, typeof(CborException))]
        [InlineData("18", -1, typeof(CborException))]
        [InlineData("19", -1, typeof(CborException))]
        [InlineData("1A", -1, typeof(CborException))]
        [InlineData("1B", -1, typeof(CborException))]
        [InlineData("1C", -1, typeof(CborException))]
        [InlineData("1D", -1, typeof(CborException))]
        [InlineData("1E", -1, typeof(CborException))]
        [InlineData("1F", -1, typeof(CborException))]
        public void ReadNegative(string hexBuffer, long expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [Theory]
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
        public void ReadPositive(string hexBuffer, ulong expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("FA4141EB85", 12.12f, null)]
        [InlineData("FAFFC00000", float.NaN, null)]
        [InlineData("FA7F800000", float.PositiveInfinity, null)]
        [InlineData("FAFF800000", float.NegativeInfinity, null)]
        public void ReadSingle(string hexBuffer, float expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("FB40283D70A3D70A3D", 12.12, null)]
        [InlineData("FBFFF8000000000000", double.NaN, null)]
        [InlineData("FB7FF0000000000000", double.PositiveInfinity, null)]
        [InlineData("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void ReadDouble(string hexBuffer, double expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("63666F6F", "foo", null)]
        [InlineData("60", "", null)]
        public void ReadString(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, (CborValue)expectedValue, expectedExceptionType);
        }

        [Fact]
        public void ReadNull()
        {
            Helper.TestRead("F6", (CborValue)CborValue.Null);
        }

        [Theory]
        [InlineData("8401020304", "1,2,3,4", null)]
        public void ReadInt32List(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            CborArray array = new CborArray(expectedValue.Split(',').Select(s => (CborValue)int.Parse(s)));
            Helper.TestRead(hexBuffer, array, expectedExceptionType);
        }

        [Theory]
        [InlineData("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void ReadStringList(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            CborArray array = new CborArray(expectedValue.Split(',').Select(s => (CborValue)s));
            Helper.TestRead(hexBuffer, array, expectedExceptionType);
        }

        [Fact]
        public void ReadObject()
        {
            const string hexBuffer =
                "A666737472696E6763666F6F666E756D626572FB40283D70A3D70A3D64626F6F6CF5646E756C6CF6656172726179820102666F626A656374A162696401";
            CborObject actualObject = Helper.Read<CborObject>(hexBuffer);
            Assert.NotNull(actualObject);

            // pairs
            Assert.Equal(6, actualObject.Count);

            // string
            Assert.True(actualObject.TryGetValue("string", out CborValue value));
            Assert.Equal(CborValueType.String, value.Type);
            Assert.IsType<CborString>(value);
            Assert.Equal("foo", value.Value<string>());

            // number
            Assert.True(actualObject.TryGetValue("number", out value));
            Assert.Equal(CborValueType.Double, value.Type);
            Assert.IsType<CborDouble>(value);
            Assert.Equal(12.12, value.Value<double>(), 3);

            // bool
            Assert.True(actualObject.TryGetValue("bool", out value));
            Assert.Equal(CborValueType.Boolean, value.Type);
            Assert.IsType<CborBoolean>(value);
            Assert.True(value.Value<bool>());

            // null
            Assert.True(actualObject.TryGetValue("null", out value));
            Assert.Equal(CborValueType.Null, value.Type);
            Assert.IsType<CborNull>(value);

            // array
            Assert.True(actualObject.TryGetValue("array", out value));
            Assert.Equal(CborValueType.Array, value.Type);
            Assert.IsType<CborArray>(value);
            CborArray CborArray = (CborArray)value;
            Assert.Equal(2, CborArray.Count);
            Assert.Equal(1, CborArray[0].Value<double>());
            Assert.Equal(2, CborArray[1].Value<double>());

            // object
            Assert.True(actualObject.TryGetValue("object", out value));
            Assert.Equal(CborValueType.Object, value.Type);
            Assert.IsType<CborObject>(value);
            CborObject cborObject = (CborObject)value;
            Assert.True(cborObject.TryGetValue("id", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(1, value.Value<int>());
        }

        [Fact]
        public void ReadArray()
        {
            string hexBuffer = "8663666F6FFB40283D70A3D70A3DF5F6820102A162696401";
            CborArray actualArray = Helper.Read<CborArray>(hexBuffer);
            Assert.NotNull(actualArray);

            // values
            Assert.Equal(6, actualArray.Count);

            // string
            CborValue actualString = actualArray[0];
            Assert.NotNull(actualString);
            Assert.Equal(CborValueType.String, actualString.Type);
            Assert.IsType<CborString>(actualString);
            Assert.Equal("foo", actualString.Value<string>());

            // number
            CborValue actualNumber = actualArray[1];
            Assert.NotNull(actualNumber);
            Assert.Equal(CborValueType.Double, actualNumber.Type);
            Assert.IsType<CborDouble>(actualNumber);
            Assert.Equal(12.12, actualNumber.Value<double>(), 3);

            // bool
            CborValue actualBool = actualArray[2];
            Assert.NotNull(actualBool);
            Assert.Equal(CborValueType.Boolean, actualBool.Type);
            Assert.IsType<CborBoolean>(actualBool);
            Assert.True(actualBool.Value<bool>());

            // null
            CborValue actualNull = actualArray[3];
            Assert.NotNull(actualNull);
            Assert.Equal(CborValueType.Null, actualNull.Type);

            // array
            CborValue actualArrayValue = actualArray[4];
            Assert.NotNull(actualArrayValue);
            Assert.Equal(CborValueType.Array, actualArrayValue.Type);
            Assert.IsType<CborArray>(actualArrayValue);
            CborArray CborArray = (CborArray)actualArrayValue;
            Assert.Equal(2, CborArray.Count);
            Assert.Equal(1, CborArray[0].Value<double>());
            Assert.Equal(2, CborArray[1].Value<double>());

            // object
            CborValue actualObject = actualArray[5];
            Assert.NotNull(actualObject);
            Assert.Equal(CborValueType.Object, actualObject.Type);
            Assert.IsType<CborObject>(actualObject);
            CborObject cborObject = (CborObject)actualObject;
            Assert.True(cborObject.TryGetValue("id", out CborValue value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(1, value.Value<int>());
        }
    }
}
