using Dahomey.Cbor.ObjectModel;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Tests
{

    public class CborWriterValueTests
    {
        [Theory]
        [InlineData("F5", true, null)]
        [InlineData("F4", false, null)]
        public void WriteBoolean(string hexBuffer, bool value, Type expectedExceptionType)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType);
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
        public void WriteNegative(string hexBuffer, long value, Type expectedExceptionType)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType);
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
        public void WritePositive(string hexBuffer, ulong value, Type expectedExceptionType)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("FA4141EB85", 12.12f, null)]
        [InlineData("F97E00", float.NaN, null)]
        [InlineData("F97C00", float.PositiveInfinity, null)]
        [InlineData("F9FC00", float.NegativeInfinity, null)]
        public void WriteSingle(string hexBuffer, float value, Type expectedExceptionType)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("FB40283D70A3D70A3D", 12.12, null)]
        [InlineData("F97E00", double.NaN, null)]
        [InlineData("F97C00", double.PositiveInfinity, null)]
        [InlineData("F9FC00", double.NegativeInfinity, null)]
        public void WriteDouble(string hexBuffer, double value, Type expectedExceptionType)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("63666F6F", "foo", null)]
        [InlineData("60", "", null)]
        public void WriteString(string hexBuffer, string value, Type expectedExceptionType)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [Fact]
        public void WriteNull()
        {
            Helper.TestWrite((CborValue)CborValue.Null, "F6");
        }

        [Theory]
        [InlineData("8401020304", "1,2,3,4", null)]
        public void WriteInt32List(string hexBuffer, string value, Type expectedExceptionType)
        {
            CborArray array = new CborArray(value.Split(',').Select(s => (CborValue)int.Parse(s)));
            Helper.TestWrite(array, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void WriteStringList(string hexBuffer, string value, Type expectedExceptionType)
        {
            CborArray array = new CborArray(value.Split(',').Select(s => (CborValue)s));
            Helper.TestWrite(array, hexBuffer, expectedExceptionType);
        }

        [Fact]
        public void WriteObject()
        {
            const string hexBuffer =
                "A666737472696E6763666F6F666E756D626572FB40283D70A3D70A3D64626F6F6CF5646E756C6CF6656172726179820102666F626A656374A162696401";
            CborObject obj = new CborObject
            {
                ["string"] = "foo",
                ["number"] = 12.12,
                ["bool"] = true,
                ["null"] = null,
                ["array"] = new CborArray {1, 2},
                ["object"] = new CborObject { [ "id" ] = 1 },
            };
            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void WriteArray()
        {
            string hexBuffer = "8663666F6FFB40283D70A3D70A3DF5F6820102A162696401";
            CborArray array = new CborArray
            {
                "foo",
                12.12,
                true,
                null,
                new CborArray {1, 2},
                new CborObject { { "id", 1 } }
            };
            Helper.TestWrite(array, hexBuffer);
        }

        [Fact]
        public void WriteEmptyArray()
        {
            const string hexBuffer = "80";

            CborArray array = new CborArray();
            
            Helper.TestWrite(array, hexBuffer);
        }
    }
}
