using Dahomey.Cbor.ObjectModel;
using Xunit;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Tests
{

    public class CborValueTests
    {
        [Fact]
        public void FromObject()
        {
            SimpleObject obj = new SimpleObject
            {
                Boolean = true,
                Byte = 12,
                SByte = 13,
                Int16 = 14,
                UInt16 = 15,
                Int32 = 16,
                UInt32 = 17u,
                Int64 = 18,
                UInt64 = 19ul,
                Single = 20.21f,
                Double = 22.23,
                Decimal = 2.71828m,
                String = "string",
                DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
                Enum = EnumTest.Value1
            };

            CborObject actual = CborObject.FromObject(obj);

            Assert.NotNull(actual);

            // pairs
            Assert.Equal(15, actual.Count);

            // Boolean
            Assert.True(actual.TryGetValue("Boolean", out CborValue value));
            Assert.Equal(CborValueType.Boolean, value.Type);
            Assert.True(value.Value<bool>());

            // Byte
            Assert.True(actual.TryGetValue("Byte", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(12, value.Value<byte>());

            // SByte
            Assert.True(actual.TryGetValue("SByte", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(13, value.Value<sbyte>());

            // Int16
            Assert.True(actual.TryGetValue("Int16", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(14, value.Value<short>());

            // UInt16
            Assert.True(actual.TryGetValue("UInt16", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(15, value.Value<ushort>());

            // Int32
            Assert.True(actual.TryGetValue("Int32", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(16, value.Value<int>());

            // UInt32
            Assert.True(actual.TryGetValue("UInt32", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(17u, value.Value<uint>());

            // Int64
            Assert.True(actual.TryGetValue("Int64", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(18L, value.Value<long>());

            // UInt64
            Assert.True(actual.TryGetValue("UInt64", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal(19ul, value.Value<ulong>());

            // Single
            Assert.True(actual.TryGetValue("Single", out value));
            Assert.Equal(CborValueType.Single, value.Type);
            Assert.Equal(20.21f, value.Value<float>());

            // UInt64
            Assert.True(actual.TryGetValue("Double", out value));
            Assert.Equal(CborValueType.Double, value.Type);
            Assert.Equal(22.23, value.Value<double>());

            // Decimal
            Assert.True(actual.TryGetValue("Decimal", out value));
            Assert.Equal(CborValueType.Decimal, value.Type);
            Assert.Equal(2.71828m, value.Value<decimal>());

            // String
            Assert.True(actual.TryGetValue("String", out value));
            Assert.Equal(CborValueType.String, value.Type);
            Assert.Equal("string", value.Value<string>());

            // DateTime
            Assert.True(actual.TryGetValue("DateTime", out value));
            Assert.Equal(CborValueType.String, value.Type);
            Assert.Equal("2014-02-21T19:00:00Z", value.Value<string>());

            // Enum
            Assert.True(actual.TryGetValue("Enum", out value));
            Assert.Equal(CborValueType.Positive, value.Type);
            Assert.Equal((int)EnumTest.Value1, value.Value<int>());
        }

        [Fact]
        public void ToObject()
        {
            CborObject obj = new CborObject
            {
                ["Boolean"] = true,
                ["Byte"] = 12,
                ["SByte"] = 13,
                ["Int16"] = 14,
                ["UInt16"] = 15,
                ["Int32"] = 16,
                ["UInt32"] = 17,
                ["Int64"] = 18,
                ["UInt64"] = 19,
                ["Single"] = 20.21f,
                ["Double"] = 22.23,
                ["Decimal"] = 2.71828m,
                ["String"] = "string",
                ["DateTime"] = "2014-02-21T19:00:00Z",
                ["Enum"] = (int)EnumTest.Value1,
            };

            SimpleObject actual = obj.ToObject<SimpleObject>();

            Assert.NotNull(obj);
            Assert.True(actual.Boolean);
            Assert.Equal(12, actual.Byte);
            Assert.Equal(13, actual.SByte);
            Assert.Equal(14, actual.Int16);
            Assert.Equal(15, actual.UInt16);
            Assert.Equal(16, actual.Int32);
            Assert.Equal(17u, actual.UInt32);
            Assert.Equal(18, actual.Int64);
            Assert.Equal(19ul, actual.UInt64);
            Assert.Equal(20.21f, actual.Single);
            Assert.Equal(22.23, actual.Double);
            Assert.Equal(2.71828m, actual.Decimal);
            Assert.Equal("string", actual.String);
            Assert.Equal(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), actual.DateTime);
            Assert.Equal(EnumTest.Value1, actual.Enum);
        }

        [Fact]
        public void ObjectComparison()
        {
            CborObject obj1 = new CborObject
            {
                ["Boolean"] = true,
                ["Byte"] = 12,
                ["SByte"] = 13,
                ["Int16"] = 14,
                ["UInt16"] = 15,
                ["Int32"] = 16,
                ["UInt32"] = 17,
                ["Int64"] = 18,
                ["UInt64"] = 19,
                ["Single"] = 20.21f,
                ["Double"] = 22.23,
                ["Decimal"] = 2.71828m,
                ["String"] = "string",
                ["DateTime"] = "2014-02-21T19:00:00Z",
                ["Enum"] = (int)EnumTest.Value1,
            };

            CborObject obj2 = new CborObject
            {
                ["Boolean"] = true,
                ["Byte"] = 12,
                ["SByte"] = 13,
                ["Int16"] = 14,
                ["UInt16"] = 15,
                ["Int32"] = 16,
                ["UInt32"] = 17,
                ["Int64"] = 18,
                ["UInt64"] = 19,
                ["Single"] = 20.21f,
                ["Double"] = 22.23,
                ["Decimal"] = 2.71828m,
                ["String"] = "string",
                ["DateTime"] = "2014-02-21T19:00:00Z",
                ["Enum"] = (int)EnumTest.Value1,
            };

            Assert.Equal(0, obj1.CompareTo(obj2));
            Assert.Equal(obj1, obj2);
        }

        [Fact]
        public void FromArray()
        {
            int[] array = { 1, 2, 3 };
            CborArray actual = CborArray.FromCollection(array);
            Assert.Equal(new CborArray(1, 2, 3), actual);
        }

        class Foo
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void FromClassArray()
        {
            Foo[] array =
            { 
                new Foo 
                { 
                    Id = 1, 
                    Name = "foo"
                }, 
                new Foo 
                { 
                    Id = 2, 
                    Name = "bar"
                }
            };

            CborArray actual = CborArray.FromCollection(array);
            CborArray expected = new CborArray(
                new CborObject
                {
                    ["Id"] = 1,
                    ["Name"] = "foo"
                },
                new CborObject
                {
                    ["Id"] = 2,
                    ["Name"] = "bar"
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FromMixedArray()
        {
            object[] array = { true, "foo", 12, 12.12 };
            CborArray actual = CborArray.FromCollection(array);
            CborArray expected = new CborArray(true, "foo", 12, 12.12);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToArray()
        {
            CborArray array = new CborArray(1, 2, 3);
            int[] actual = array.ToCollection<int[]>();
            Assert.Equal(new[] { 1, 2, 3 }, actual);
        }

        [Fact]
        public void FromList()
        {
            List<int> list = new List<int> { 1, 2, 3 };
            CborArray actual = CborArray.FromCollection(list);
            Assert.Equal(new CborArray(1, 2, 3), actual);
        }

        [Fact]
        public void FromClassList()
        {
            List<Foo> list = new List<Foo>
            {
                new Foo
                {
                    Id = 1,
                    Name = "foo"
                },
                new Foo
                {
                    Id = 2,
                    Name = "bar"
                }
            };

            CborArray actual = CborArray.FromCollection(list);
            CborArray expected = new CborArray(
                new CborObject
                {
                    ["Id"] = 1,
                    ["Name"] = "foo"
                },
                new CborObject
                {
                    ["Id"] = 2,
                    ["Name"] = "bar"
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FromMixedList()
        {
            List<object> list = new List<object> { true, "foo", 12, 12.12 };
            CborArray actual = CborArray.FromCollection(list);
            CborArray expected = new CborArray(true, "foo", 12, 12.12);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToList()
        {
            CborArray array = new CborArray(1, 2, 3);
            List<int> actual = array.ToCollection<List<int>>();
            Assert.Equal(new List<int> { 1, 2, 3 }, actual);
        }

        [Fact]
        public void ArrayComparison()
        {
            CborArray array1 = new CborArray(1, 2, 3);
            CborArray array2 = new CborArray(1, 2, 3);

            Assert.Equal(0, array1.CompareTo(array2));
            Assert.Equal(array1, array2);
        }
    }
}
