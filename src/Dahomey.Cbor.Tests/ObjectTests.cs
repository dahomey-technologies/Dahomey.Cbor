using Dahomey.Cbor.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class ObjectTests
    {
        static ObjectTests()
        {
            SampleClasses.Initialize();
        }

        [TestMethod]
        public void ReadSimpleObject()
        {
            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            SimpleObject obj = Helper.Read<SimpleObject>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.Boolean);
            Assert.AreEqual(12, obj.Byte);
            Assert.AreEqual(13, obj.SByte);
            Assert.AreEqual(14, obj.Int16);
            Assert.AreEqual(15, obj.UInt16);
            Assert.AreEqual(16, obj.Int32);
            Assert.AreEqual(17u, obj.UInt32);
            Assert.AreEqual(18, obj.Int64);
            Assert.AreEqual(19ul, obj.UInt64);
            Assert.AreEqual(20.21f, obj.Single);
            Assert.AreEqual(22.23, obj.Double);
            Assert.AreEqual("string", obj.String);
            Assert.AreEqual(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.AreEqual(EnumTest.Value1, obj.Enum);
        }

        [TestMethod]
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
                String = "string",
                DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
                Enum = EnumTest.Value1
            };

            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            Helper.TestWrite(obj, hexBuffer, null, new CborOptions { EnumFormat = ValueFormat.WriteToString });
        }

        [TestMethod]
        public void ReadSimpleObjectWithFields()
        {
            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            SimpleObjectWithFields obj = Helper.Read<SimpleObjectWithFields>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.Boolean);
            Assert.AreEqual(12, obj.Byte);
            Assert.AreEqual(13, obj.SByte);
            Assert.AreEqual(14, obj.Int16);
            Assert.AreEqual(15, obj.UInt16);
            Assert.AreEqual(16, obj.Int32);
            Assert.AreEqual(17u, obj.UInt32);
            Assert.AreEqual(18, obj.Int64);
            Assert.AreEqual(19ul, obj.UInt64);
            Assert.AreEqual(20.21f, obj.Single);
            Assert.AreEqual(22.23, obj.Double);
            Assert.AreEqual("string", obj.String);
            Assert.AreEqual(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.AreEqual(EnumTest.Value1, obj.Enum);
        }

        [TestMethod]
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
                String = "string",
                DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
                Enum = EnumTest.Value1
            };

            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            Helper.TestWrite(obj, hexBuffer, null, new CborOptions { EnumFormat = ValueFormat.WriteToString });
        }

        [DataTestMethod]
        [DataRow("A16943626F7256616C756501", CborValueType.Positive)]
        [DataRow("A16943626F7256616C756563666F6F", CborValueType.String)]
        [DataRow("A16943626F7256616C7565F4", CborValueType.Boolean)]
        [DataRow("A16943626F7256616C7565F6", CborValueType.Null)]
        [DataRow("A16943626F7256616C7565A26269640166C3A9C3A0C3AFF4", CborValueType.Object)]
        [DataRow("A16943626F7256616C75658301F5F6", CborValueType.Array)]
        public void ReadWithCborValue(string hexBuffer, CborValueType expectedCborValueType)
        {
            CborValueObject CborValueObject = Helper.Read<CborValueObject>(hexBuffer);
            Assert.IsNotNull(CborValueObject);
            Assert.IsNotNull(CborValueObject.CborValue);
            Assert.AreEqual(expectedCborValueType, CborValueObject.CborValue.Type);
        }

        [TestMethod]
        public void WriteWithCborValue()
        {
            const string hexBuffer = "A16943626F7256616C75658301F5F6";

            CborValueObject obj = new CborValueObject
            {
                CborValue = new CborArray(1, true, null)
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void ReadObjectWithList()
        {
            const string hexBuffer = "A367496E744C6973748201026A4F626A6563744C69737482A168496E7456616C756501A168496E7456616C7565026A537472696E674C6973748261616162";
            ListObject obj = Helper.Read<ListObject>(hexBuffer);

            Assert.IsNotNull(obj);

            Assert.IsNotNull(obj.IntList);
            Assert.AreEqual(2, obj.IntList.Count);
            Assert.AreEqual(1, obj.IntList[0]);
            Assert.AreEqual(2, obj.IntList[1]);

            Assert.IsNotNull(obj.ObjectList);
            Assert.AreEqual(2, obj.ObjectList.Count);
            Assert.IsNotNull(obj.ObjectList[0]);
            Assert.AreEqual(1, obj.ObjectList[0].IntValue);
            Assert.IsNotNull(obj.ObjectList[1]);
            Assert.AreEqual(2, obj.ObjectList[1].IntValue);

            Assert.IsNotNull(obj.StringList);
            Assert.AreEqual(2, obj.StringList.Count);
            Assert.AreEqual("a", obj.StringList[0]);
            Assert.AreEqual("b", obj.StringList[1]);
        }

        [TestMethod]
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

        [TestMethod]
        public void ReadWithArray()
        {
            const string hexBuffer = "A368496E7441727261798201026B4F626A656374417272617982A168496E7456616C756501A168496E7456616C7565026B537472696E6741727261798261616162";
            ArrayObject obj = Helper.Read<ArrayObject>(hexBuffer);

            Assert.IsNotNull(obj);

            Assert.IsNotNull(obj.IntArray);
            Assert.AreEqual(2, obj.IntArray.Length);
            Assert.AreEqual(1, obj.IntArray[0]);
            Assert.AreEqual(2, obj.IntArray[1]);

            Assert.IsNotNull(obj.ObjectArray);
            Assert.AreEqual(2, obj.ObjectArray.Length);
            Assert.IsNotNull(obj.ObjectArray[0]);
            Assert.AreEqual(1, obj.ObjectArray[0].IntValue);
            Assert.IsNotNull(obj.ObjectArray[1]);
            Assert.AreEqual(2, obj.ObjectArray[1].IntValue);

            Assert.IsNotNull(obj.StringArray);
            Assert.AreEqual(2, obj.StringArray.Length);
            Assert.AreEqual("a", obj.StringArray[0]);
            Assert.AreEqual("b", obj.StringArray[1]);
        }

        [TestMethod]
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

        [TestMethod]
        public void ReadWithDictionary()
        {
            const string hexBuffer = "A46D496E7444696374696F6E617279A2010102026E55496E7444696374696F6E617279A201A168496E7456616C75650102A168496E7456616C75650270537472696E6744696374696F6E617279A2613182A168496E7456616C75650BA168496E7456616C75650C613282A168496E7456616C756515A168496E7456616C7565166E456E756D44696374696F6E617279A26656616C756531A201A168496E7456616C75650B02A168496E7456616C75650C6656616C756532A201A168496E7456616C75651502A168496E7456616C756516";
            DictionaryObject obj = Helper.Read<DictionaryObject>(hexBuffer);

            Assert.IsNotNull(obj);

            Assert.IsNotNull(obj.IntDictionary);
            Assert.AreEqual(2, obj.IntDictionary.Count);
            Assert.IsTrue(obj.IntDictionary.ContainsKey(1));
            Assert.AreEqual(1, obj.IntDictionary[1]);
            Assert.IsTrue(obj.IntDictionary.ContainsKey(2));
            Assert.AreEqual(2, obj.IntDictionary[2]);

            Assert.IsNotNull(obj.UIntDictionary);
            Assert.AreEqual(2, obj.UIntDictionary.Count);
            Assert.IsTrue(obj.UIntDictionary.ContainsKey(1));
            Assert.IsNotNull(obj.UIntDictionary[1]);
            Assert.AreEqual(1, obj.UIntDictionary[1].IntValue);
            Assert.IsTrue(obj.UIntDictionary.ContainsKey(2));
            Assert.IsNotNull(obj.UIntDictionary[2]);
            Assert.AreEqual(2, obj.UIntDictionary[2].IntValue);

            Assert.IsNotNull(obj.StringDictionary);
            Assert.AreEqual(2, obj.StringDictionary.Count);
            Assert.IsTrue(obj.StringDictionary.ContainsKey("1"));
            Assert.IsNotNull(obj.StringDictionary["1"]);
            Assert.AreEqual(2, obj.StringDictionary["1"].Count);
            Assert.AreEqual(11, obj.StringDictionary["1"][0].IntValue);
            Assert.AreEqual(12, obj.StringDictionary["1"][1].IntValue);
            Assert.IsTrue(obj.StringDictionary.ContainsKey("2"));
            Assert.IsNotNull(obj.StringDictionary["2"]);
            Assert.AreEqual(2, obj.StringDictionary["2"].Count);
            Assert.AreEqual(21, obj.StringDictionary["2"][0].IntValue);
            Assert.AreEqual(22, obj.StringDictionary["2"][1].IntValue);

            Assert.IsNotNull(obj.EnumDictionary);
            Assert.AreEqual(2, obj.EnumDictionary.Count);
            Assert.IsTrue(obj.EnumDictionary.ContainsKey(EnumTest.Value1));
            Assert.IsNotNull(obj.EnumDictionary[EnumTest.Value1]);
            Assert.AreEqual(2, obj.EnumDictionary[EnumTest.Value1].Count);
            Assert.AreEqual(11, obj.EnumDictionary[EnumTest.Value1][1].IntValue);
            Assert.AreEqual(12, obj.EnumDictionary[EnumTest.Value1][2].IntValue);
            Assert.IsTrue(obj.EnumDictionary.ContainsKey(EnumTest.Value2));
            Assert.IsNotNull(obj.EnumDictionary[EnumTest.Value2]);
            Assert.AreEqual(2, obj.EnumDictionary[EnumTest.Value2].Count);
            Assert.AreEqual(21, obj.EnumDictionary[EnumTest.Value2][1].IntValue);
            Assert.AreEqual(22, obj.EnumDictionary[EnumTest.Value2][2].IntValue);
        }

        [TestMethod]
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

        [TestMethod]
        public void ReadWithHashSet()
        {
            const string hexBuffer = "A36A496E74486173685365748201026D4F626A6563744861736853657482A168496E7456616C756501A168496E7456616C7565026D537472696E67486173685365748261616162";
            HashSetObject obj = Helper.Read<HashSetObject>(hexBuffer);

            Assert.IsNotNull(obj);

            Assert.IsNotNull(obj.IntHashSet);
            Assert.AreEqual(2, obj.IntHashSet.Count);
            Assert.IsTrue(obj.IntHashSet.Contains(1));
            Assert.IsTrue(obj.IntHashSet.Contains(2));

            Assert.IsNotNull(obj.ObjectHashSet);
            Assert.AreEqual(2, obj.ObjectHashSet.Count);
            Assert.IsTrue(obj.ObjectHashSet.Contains(new IntObject { IntValue = 1 }));
            Assert.IsTrue(obj.ObjectHashSet.Contains(new IntObject { IntValue = 2 }));

            Assert.IsNotNull(obj.StringHashSet);
            Assert.AreEqual(2, obj.StringHashSet.Count);
            Assert.IsTrue(obj.StringHashSet.Contains("a"));
            Assert.IsTrue(obj.StringHashSet.Contains("b"));
        }

        [TestMethod]
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

        [TestMethod]
        public void ReadWithObject()
        {
            const string hexBuffer = "A1664F626A656374A168496E7456616C75650C";
            ObjectWithObject obj = Helper.Read<ObjectWithObject>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.Object);
            Assert.AreEqual(12, obj.Object.IntValue);
        }

        [TestMethod]
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

        [TestMethod]
        public void ReadGenericObject()
        {
            const string hexBuffer = "A16556616C756501";
            GenericObject<int> obj = Helper.Read<GenericObject<int>>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(1, obj.Value);
        }

        [TestMethod]
        public void WriteGenericObject()
        {
            const string hexBuffer = "A16556616C756501";

            GenericObject<int> obj = new GenericObject<int>
            {
                Value = 1
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void ReadWithCborProperty()
        {
            const string hexBuffer = "A16269640C";
            ObjectWithCborProperty obj = Helper.Read<ObjectWithCborProperty>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(12, obj.Id);
        }

        [TestMethod]
        public void WriteWithCborProperty()
        {
            const string hexBuffer = "A16269640C";

            ObjectWithCborProperty obj = new ObjectWithCborProperty
            {
                Id = 12
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void ReadWithNamingConvention()
        {
            const string hexBuffer = "A1676D7956616C75650C";
            ObjectWithNamingConvention obj = Helper.Read<ObjectWithNamingConvention>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(12, obj.MyValue);
        }

        [TestMethod]
        public void ReadPolymorphicObject()
        {
            const string hexBuffer = "A16A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F62496401";
            BaseObjectHolder obj = Helper.Read<BaseObjectHolder>(hexBuffer);
        }

        [TestMethod]
        public void WritePolymorphicObject()
        {
            const string hexBuffer = "A16A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F62496401";
            BaseObjectHolder obj = new BaseObjectHolder
            {
                BaseObject = new NameObject
                {
                    Id = 1,
                    Name = "foo"
                }
            };
            Helper.TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void ReadWithCustomConverterOnProperty()
        {
            const string hexBuffer = "A16447756964505DF4EB676C01484B8AE61E389127B717";
            ObjectWithCustomConverterOnProperty obj = Helper.Read<ObjectWithCustomConverterOnProperty>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(Guid.Parse("67EBF45D-016C-4B48-8AE6-1E389127B717"), obj.Guid);
        }

        [TestMethod]
        public void WriteWithCustomConverterOnProperty()
        {
            const string hexBuffer = "A16447756964505DF4EB676C01484B8AE61E389127B717";
            ObjectWithCustomConverterOnProperty obj = new ObjectWithCustomConverterOnProperty
            {
                Guid = Guid.Parse("67EBF45D-016C-4B48-8AE6-1E389127B717")
            };
            Helper.TestWrite(obj, hexBuffer);
        }

        [DataRow("A261610168496E7456616C756501")]
        [DataRow("A268496E7456616C756501616101")]
        [DataRow("A26161810C68496E7456616C756501")]
        [DataRow("A268496E7456616C7565016161810C")]
        [DataRow("A2616163666F6F68496E7456616C756501")]
        [DataRow("A268496E7456616C756501616163666F6F")]
        [DataRow("A26161A161610168496E7456616C756501")]
        [DataRow("A268496E7456616C7565016161A1616101")]
        [DataRow("A268496E7456616C7565016161F5")]
        [DataRow("A26161F568496E7456616C756501")]
        [DataRow("A268496E7456616C7565016161F6")]
        [DataRow("A268496E7456616C7565016161F93800")]
        [DataRow("A26161F9380068496E7456616C756501")]
        [DataRow("A268496E7456616C7565016161FB3FC999999999999A")]
        public void UnhandledNameTest(string hexBuffer)
        {
            IntObject obj = Helper.Read<IntObject>(hexBuffer);
            Assert.IsNotNull(obj);
            Assert.AreEqual(1, obj.IntValue);
        }
    }
}
