using Dahomey.Cbor.ObjectModel;
using Xunit;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Tests
{

    public class ObjectTests
    {
        static ObjectTests()
        {
            SampleClasses.Initialize();
        }

        [Fact]
        public void ReadSimpleObject()
        {
            const string hexBuffer = "AF67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B67446563696D616CFC00000000000425D40000000000050000684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            SimpleObject obj = Helper.Read<SimpleObject>(hexBuffer);

            Assert.NotNull(obj);
            Assert.True(obj.Boolean);
            Assert.Equal(12, obj.Byte);
            Assert.Equal(13, obj.SByte);
            Assert.Equal(14, obj.Int16);
            Assert.Equal(15, obj.UInt16);
            Assert.Equal(16, obj.Int32);
            Assert.Equal(17u, obj.UInt32);
            Assert.Equal(18, obj.Int64);
            Assert.Equal(19ul, obj.UInt64);
            Assert.Equal(20.21f, obj.Single);
            Assert.Equal(22.23, obj.Double);
            Assert.Equal(2.71828m, obj.Decimal);
            Assert.Equal("string", obj.String);
            Assert.Equal(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.Equal(EnumTest.Value1, obj.Enum);
        }

        [Fact]
        public void WriteSimpleObject()
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

            const string hexBuffer = "AF67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B67446563696D616CFC00000000000425D40000000000050000684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            Helper.TestWrite(obj, hexBuffer, null, new CborOptions { EnumFormat = ValueFormat.WriteToString });
        }

        [Fact]
        public void ReadSimpleObjectWithFields()
        {
            const string hexBuffer = "AF67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B67446563696D616CFC00000000000425D40000000000050000684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            SimpleObjectWithFields obj = Helper.Read<SimpleObjectWithFields>(hexBuffer);

            Assert.NotNull(obj);
            Assert.True(obj.Boolean);
            Assert.Equal(12, obj.Byte);
            Assert.Equal(13, obj.SByte);
            Assert.Equal(14, obj.Int16);
            Assert.Equal(15, obj.UInt16);
            Assert.Equal(16, obj.Int32);
            Assert.Equal(17u, obj.UInt32);
            Assert.Equal(18, obj.Int64);
            Assert.Equal(19ul, obj.UInt64);
            Assert.Equal(20.21f, obj.Single);
            Assert.Equal(22.23, obj.Double);
            Assert.Equal(2.71828m, obj.Decimal);
            Assert.Equal("string", obj.String);
            Assert.Equal(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.Equal(EnumTest.Value1, obj.Enum);
        }

        [Fact]
        public void WriteSimpleObjectWithFields()
        {
            SimpleObjectWithFields obj = new SimpleObjectWithFields
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

            const string hexBuffer = "AF67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B67446563696D616CFC00000000000425D40000000000050000684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            Helper.TestWrite(obj, hexBuffer, null, new CborOptions { EnumFormat = ValueFormat.WriteToString });
        }

        [Theory]
        [InlineData("A16943626F7256616C756501", CborValueType.Positive)]
        [InlineData("A16943626F7256616C756563666F6F", CborValueType.String)]
        [InlineData("A16943626F7256616C7565F4", CborValueType.Boolean)]
        [InlineData("A16943626F7256616C7565F6", CborValueType.Null)]
        [InlineData("A16943626F7256616C7565A26269640166C3A9C3A0C3AFF4", CborValueType.Object)]
        [InlineData("A16943626F7256616C75658301F5F6", CborValueType.Array)]
        public void ReadWithCborValue(string hexBuffer, CborValueType expectedCborValueType)
        {
            CborValueObject CborValueObject = Helper.Read<CborValueObject>(hexBuffer);
            Assert.NotNull(CborValueObject);
            Assert.NotNull(CborValueObject.CborValue);
            Assert.Equal(expectedCborValueType, CborValueObject.CborValue.Type);
        }

        [Fact]
        public void WriteWithCborValue()
        {
            const string hexBuffer = "A16943626F7256616C75658301F5F6";

            CborValueObject obj = new CborValueObject
            {
                CborValue = new CborArray(1, true, null)
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadObjectWithList()
        {
            const string hexBuffer = "A367496E744C6973748201026A4F626A6563744C69737482A168496E7456616C756501A168496E7456616C7565026A537472696E674C6973748261616162";
            ListObject obj = Helper.Read<ListObject>(hexBuffer);

            Assert.NotNull(obj);

            Assert.NotNull(obj.IntList);
            Assert.Equal(2, obj.IntList.Count);
            Assert.Equal(1, obj.IntList[0]);
            Assert.Equal(2, obj.IntList[1]);

            Assert.NotNull(obj.ObjectList);
            Assert.Equal(2, obj.ObjectList.Count);
            Assert.NotNull(obj.ObjectList[0]);
            Assert.Equal(1, obj.ObjectList[0].IntValue);
            Assert.NotNull(obj.ObjectList[1]);
            Assert.Equal(2, obj.ObjectList[1].IntValue);

            Assert.NotNull(obj.StringList);
            Assert.Equal(2, obj.StringList.Count);
            Assert.Equal("a", obj.StringList[0]);
            Assert.Equal("b", obj.StringList[1]);
        }

        [Fact]
        public void WriteWithList()
        {
            const string hexBuffer = "A367496E744C6973748201026A4F626A6563744C69737482A168496E7456616C756501A168496E7456616C7565026A537472696E674C6973748261616162";

            ListObject obj = new ListObject
            {
                IntList = new List<int> { 1, 2 },
                ObjectList = new List<IntObject>
                {
                    new IntObject { IntValue = 1 },
                    new IntObject { IntValue = 2 }
                },
                StringList = new List<string> { "a", "b" }
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadWithArray()
        {
            const string hexBuffer = "A368496E7441727261798201026B4F626A656374417272617982A168496E7456616C756501A168496E7456616C7565026B537472696E6741727261798261616162";
            ArrayObject obj = Helper.Read<ArrayObject>(hexBuffer);

            Assert.NotNull(obj);

            Assert.NotNull(obj.IntArray);
            Assert.Equal(2, obj.IntArray.Length);
            Assert.Equal(1, obj.IntArray[0]);
            Assert.Equal(2, obj.IntArray[1]);

            Assert.NotNull(obj.ObjectArray);
            Assert.Equal(2, obj.ObjectArray.Length);
            Assert.NotNull(obj.ObjectArray[0]);
            Assert.Equal(1, obj.ObjectArray[0].IntValue);
            Assert.NotNull(obj.ObjectArray[1]);
            Assert.Equal(2, obj.ObjectArray[1].IntValue);

            Assert.NotNull(obj.StringArray);
            Assert.Equal(2, obj.StringArray.Length);
            Assert.Equal("a", obj.StringArray[0]);
            Assert.Equal("b", obj.StringArray[1]);
        }

        [Fact]
        public void WriteWithArray()
        {
            const string hexBuffer = "A368496E7441727261798201026B4F626A656374417272617982A168496E7456616C756501A168496E7456616C7565026B537472696E6741727261798261616162";

            ArrayObject obj = new ArrayObject
            {
                IntArray = new[] { 1, 2 },
                ObjectArray = new[]
                {
                    new IntObject { IntValue = 1 },
                    new IntObject { IntValue = 2 }
                },
                StringArray = new[] { "a", "b" }
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadWithDictionary()
        {
            const string hexBuffer = "A46D496E7444696374696F6E617279A2010102026E55496E7444696374696F6E617279A201A168496E7456616C75650102A168496E7456616C75650270537472696E6744696374696F6E617279A2613182A168496E7456616C75650BA168496E7456616C75650C613282A168496E7456616C756515A168496E7456616C7565166E456E756D44696374696F6E617279A26656616C756531A201A168496E7456616C75650B02A168496E7456616C75650C6656616C756532A201A168496E7456616C75651502A168496E7456616C756516";
            DictionaryObject obj = Helper.Read<DictionaryObject>(hexBuffer);

            Assert.NotNull(obj);

            Assert.NotNull(obj.IntDictionary);
            Assert.Equal(2, obj.IntDictionary.Count);
            Assert.True(obj.IntDictionary.ContainsKey(1));
            Assert.Equal(1, obj.IntDictionary[1]);
            Assert.True(obj.IntDictionary.ContainsKey(2));
            Assert.Equal(2, obj.IntDictionary[2]);

            Assert.NotNull(obj.UIntDictionary);
            Assert.Equal(2, obj.UIntDictionary.Count);
            Assert.True(obj.UIntDictionary.ContainsKey(1));
            Assert.NotNull(obj.UIntDictionary[1]);
            Assert.Equal(1, obj.UIntDictionary[1].IntValue);
            Assert.True(obj.UIntDictionary.ContainsKey(2));
            Assert.NotNull(obj.UIntDictionary[2]);
            Assert.Equal(2, obj.UIntDictionary[2].IntValue);

            Assert.NotNull(obj.StringDictionary);
            Assert.Equal(2, obj.StringDictionary.Count);
            Assert.True(obj.StringDictionary.ContainsKey("1"));
            Assert.NotNull(obj.StringDictionary["1"]);
            Assert.Equal(2, obj.StringDictionary["1"].Count);
            Assert.Equal(11, obj.StringDictionary["1"][0].IntValue);
            Assert.Equal(12, obj.StringDictionary["1"][1].IntValue);
            Assert.True(obj.StringDictionary.ContainsKey("2"));
            Assert.NotNull(obj.StringDictionary["2"]);
            Assert.Equal(2, obj.StringDictionary["2"].Count);
            Assert.Equal(21, obj.StringDictionary["2"][0].IntValue);
            Assert.Equal(22, obj.StringDictionary["2"][1].IntValue);

            Assert.NotNull(obj.EnumDictionary);
            Assert.Equal(2, obj.EnumDictionary.Count);
            Assert.True(obj.EnumDictionary.ContainsKey(EnumTest.Value1));
            Assert.NotNull(obj.EnumDictionary[EnumTest.Value1]);
            Assert.Equal(2, obj.EnumDictionary[EnumTest.Value1].Count);
            Assert.Equal(11, obj.EnumDictionary[EnumTest.Value1][1].IntValue);
            Assert.Equal(12, obj.EnumDictionary[EnumTest.Value1][2].IntValue);
            Assert.True(obj.EnumDictionary.ContainsKey(EnumTest.Value2));
            Assert.NotNull(obj.EnumDictionary[EnumTest.Value2]);
            Assert.Equal(2, obj.EnumDictionary[EnumTest.Value2].Count);
            Assert.Equal(21, obj.EnumDictionary[EnumTest.Value2][1].IntValue);
            Assert.Equal(22, obj.EnumDictionary[EnumTest.Value2][2].IntValue);
        }

        [Fact]
        public void WriteWithDictionary()
        {
            const string hexBuffer = "A46D496E7444696374696F6E617279A2010102026E55496E7444696374696F6E617279A201A168496E7456616C75650102A168496E7456616C75650270537472696E6744696374696F6E617279A2613182A168496E7456616C75650BA168496E7456616C75650C613282A168496E7456616C756515A168496E7456616C7565166E456E756D44696374696F6E617279A26656616C756531A201A168496E7456616C75650B02A168496E7456616C75650C6656616C756532A201A168496E7456616C75651502A168496E7456616C756516";

            DictionaryObject obj = new DictionaryObject
            {
                IntDictionary = new Dictionary<int, int>
                {
                    { 1, 1 },
                    { 2, 2 }
                },
                UIntDictionary = new Dictionary<uint, IntObject>
                {
                    { 1, new IntObject { IntValue = 1 } },
                    { 2, new IntObject { IntValue = 2 } }
                },
                StringDictionary = new Dictionary<string, List<IntObject>>
                {
                    { "1", new List<IntObject> { new IntObject { IntValue = 11 }, new IntObject { IntValue = 12 } } },
                    { "2", new List<IntObject> { new IntObject { IntValue = 21 }, new IntObject { IntValue = 22 } } }
                },
                EnumDictionary = new Dictionary<EnumTest, Dictionary<int, IntObject>>
                {
                    {
                        EnumTest.Value1, new Dictionary<int, IntObject>
                        {
                            { 1, new IntObject { IntValue = 11 } },
                            { 2, new IntObject { IntValue = 12 } }
                        }
                    },
                    {
                        EnumTest.Value2, new Dictionary<int, IntObject>
                        {
                            { 1, new IntObject { IntValue = 21 } },
                            { 2, new IntObject { IntValue = 22 } }
                        }
                    }
                }
            };

            Helper.TestWrite(obj, hexBuffer, null,
                new CborOptions { EnumFormat = ValueFormat.WriteToString });
        }

        [Fact]
        public void ReadWithHashSet()
        {
            const string hexBuffer = "A36A496E74486173685365748201026D4F626A6563744861736853657482A168496E7456616C756501A168496E7456616C7565026D537472696E67486173685365748261616162";
            HashSetObject obj = Helper.Read<HashSetObject>(hexBuffer);

            Assert.NotNull(obj);

            Assert.NotNull(obj.IntHashSet);
            Assert.Equal(2, obj.IntHashSet.Count);
            Assert.Contains(1, obj.IntHashSet);
            Assert.Contains(2, obj.IntHashSet);

            Assert.NotNull(obj.ObjectHashSet);
            Assert.Equal(2, obj.ObjectHashSet.Count);
            Assert.Contains(new IntObject { IntValue = 1 }, obj.ObjectHashSet);
            Assert.Contains(new IntObject { IntValue = 2 }, obj.ObjectHashSet);

            Assert.NotNull(obj.StringHashSet);
            Assert.Equal(2, obj.StringHashSet.Count);
            Assert.Contains("a", obj.StringHashSet);
            Assert.Contains("b", obj.StringHashSet);
        }

        [Fact]
        public void WriteWithHashSet()
        {
            const string hexBuffer = "A36A496E74486173685365748201026D4F626A6563744861736853657482A168496E7456616C756501A168496E7456616C7565026D537472696E67486173685365748261616162";

            HashSetObject obj = new HashSetObject
            {
                IntHashSet = new HashSet<int> { 1, 2 },
                ObjectHashSet = new HashSet<IntObject>
                {
                    new IntObject { IntValue = 1 },
                    new IntObject { IntValue = 2 }
                },
                StringHashSet = new HashSet<string> { "a", "b" }
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadWithObject()
        {
            const string hexBuffer = "A1664F626A656374A168496E7456616C75650C";
            ObjectWithObject obj = Helper.Read<ObjectWithObject>(hexBuffer);

            Assert.NotNull(obj);
            Assert.NotNull(obj.Object);
            Assert.Equal(12, obj.Object.IntValue);
        }

        [Fact]
        public void WriteWithObject()
        {
            const string hexBuffer = "A1664F626A656374A168496E7456616C75650C";

            ObjectWithObject obj = new ObjectWithObject
            {
                Object = new IntObject
                {
                    IntValue = 12
                }
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadGenericObject()
        {
            const string hexBuffer = "A16556616C756501";
            GenericObject<int> obj = Helper.Read<GenericObject<int>>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(1, obj.Value);
        }

        [Fact]
        public void WriteGenericObject()
        {
            const string hexBuffer = "A16556616C756501";

            GenericObject<int> obj = new GenericObject<int>
            {
                Value = 1
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadWithCborProperty()
        {
            const string hexBuffer = "A16269640C";
            ObjectWithCborProperty obj = Helper.Read<ObjectWithCborProperty>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
        }

        [Fact]
        public void WriteWithCborProperty()
        {
            const string hexBuffer = "A16269640C";

            ObjectWithCborProperty obj = new ObjectWithCborProperty
            {
                Id = 12
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void ReadWithNamingConvention()
        {
            const string hexBuffer = "A1676D7956616C75650C";
            ObjectWithNamingConvention obj = Helper.Read<ObjectWithNamingConvention>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.MyValue);
        }

        [Fact]
        public void ReadWithCustomConverterOnProperty()
        {
            const string hexBuffer = "A16447756964505DF4EB676C01484B8AE61E389127B717";
            ObjectWithCustomConverterOnProperty obj = Helper.Read<ObjectWithCustomConverterOnProperty>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(Guid.Parse("67EBF45D-016C-4B48-8AE6-1E389127B717"), obj.Guid);
        }

        [Fact]
        public void WriteWithCustomConverterOnProperty()
        {
            const string hexBuffer = "A16447756964505DF4EB676C01484B8AE61E389127B717";
            ObjectWithCustomConverterOnProperty obj = new ObjectWithCustomConverterOnProperty
            {
                Guid = Guid.Parse("67EBF45D-016C-4B48-8AE6-1E389127B717")
            };
            Helper.TestWrite(obj, hexBuffer);
        }

        [Theory]
        [InlineData("A261610168496E7456616C756501")]
        [InlineData("A268496E7456616C756501616101")]
        [InlineData("A26161810C68496E7456616C756501")]
        [InlineData("A268496E7456616C7565016161810C")]
        [InlineData("A2616163666F6F68496E7456616C756501")]
        [InlineData("A268496E7456616C756501616163666F6F")]
        [InlineData("A26161A161610168496E7456616C756501")]
        [InlineData("A268496E7456616C7565016161A1616101")]
        [InlineData("A268496E7456616C7565016161F5")]
        [InlineData("A26161F568496E7456616C756501")]
        [InlineData("A268496E7456616C7565016161F6")]
        [InlineData("A268496E7456616C7565016161F93800")]
        [InlineData("A26161F9380068496E7456616C756501")]
        [InlineData("A268496E7456616C7565016161FB3FC999999999999A")]
        public void UnhandledNameTest(string hexBuffer)
        {
            IntObject obj = Helper.Read<IntObject>(hexBuffer);
            Assert.NotNull(obj);
            Assert.Equal(1, obj.IntValue);
        }
    }
}
