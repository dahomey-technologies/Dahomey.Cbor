using Dahomey.Cbor.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborReaderTests
    {
        static CborReaderTests()
        {
            SampleClasses.Initialize();
        }

        [DataTestMethod]
        [DataRow("F5", true, null)]
        [DataRow("F4", false, null)]
        public void ReadBoolean(string hexBuffer, bool expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
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
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("18FF", byte.MaxValue, null)]
        [DataRow("0C", (byte)12, null)]
        [DataRow("2B", (byte)0, typeof(CborException))]
        [DataRow("00", (byte)0, null)]
        [DataRow("61", (byte)0, typeof(CborException))]
        public void ReadByte(string hexBuffer, byte expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
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
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("19FFFF", ushort.MaxValue, null)]
        [DataRow("0C", (ushort)12, null)]
        [DataRow("2B", (ushort)0, typeof(CborException))]
        [DataRow("00", (ushort)0, null)]
        [DataRow("61", (ushort)0, typeof(CborException))]
        public void ReadUInt16(string hexBuffer, ushort expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
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
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1AFFFFFFFF", uint.MaxValue, null)]
        [DataRow("0C", 12u, null)]
        [DataRow("2B", 0u, typeof(CborException))]
        [DataRow("00", 0u, null)]
        [DataRow("61", 0u, typeof(CborException))]
        public void ReadUInt32(string hexBuffer, uint expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
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
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
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
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FA4141EB85", 12.12f, null)]
        [DataRow("FAFFC00000", float.NaN, null)]
        [DataRow("FA7F800000", float.PositiveInfinity, null)]
        [DataRow("FAFF800000", float.NegativeInfinity, null)]
        public void ReadSingle(string hexBuffer, float expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("FB40283D70A3D70A3D", 12.12, null)]
        [DataRow("FBFFF8000000000000", double.NaN, null)]
        [DataRow("FB7FF0000000000000", double.PositiveInfinity, null)]
        [DataRow("FBFFF0000000000000", double.NegativeInfinity, null)]
        public void ReadDouble(string hexBuffer, double expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("63666F6F", "foo", null)]
        [DataRow("7F6166616F616FFF", "foo", typeof(NotSupportedException))]
        [DataRow("F6", null, null)]
        [DataRow("60", "", null)]
        public void ReadString(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1A4BFBAFFA", "2010-05-25T11:09:46Z")]
        [DataRow("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z")]
        public void ReadDateTime(string hexBuffer, string expectedISO8601)
        {
            DateTime actualDateTime = Helper.Read<DateTime>(hexBuffer);
            DateTime expectedDateTime = DateTime.ParseExact(expectedISO8601,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.AreEqual(expectedDateTime, actualDateTime);
        }

        [DataTestMethod]
        [DataRow("00", EnumTest.None, null)]
        [DataRow("01", EnumTest.Value1, null)]
        [DataRow("02", EnumTest.Value2, null)]
        [DataRow("03", (EnumTest)3, null)]
        [DataRow("644E6F6E65", EnumTest.None, null)]
        [DataRow("6656616C756531", EnumTest.Value1, null)]
        [DataRow("6656616C756532", EnumTest.Value2, null)]
        [DataRow("67496E76616C6964", (EnumTest)0, typeof(CborException))]
        public void ReadEnum(string hexBuffer, EnumTest expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        [DataRow("9F01020304FF", "1,2,3,4", null)]
        public void ReadInt32List(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            List<int> expectedList = expectedValue.Split(',').Select(int.Parse).ToList();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("A201010202", "1:1,2:2", null)]
        [DataRow("BF01010202FF", "1:1,2:2", null)]
        public void ReadInt32Dictionary(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Dictionary<int, int> expectedDict = expectedValue
                .Split(',').Select(s => s.Split(':').Select(int.Parse).ToArray())
                .ToDictionary(i => i[0], i => i[1]);
            Helper.TestRead(hexBuffer, expectedDict, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void ReadStringList(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            List<string> expectedList = expectedValue.Split(',').ToList();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void ReadInt32Array(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            int[] expectedList = expectedValue.Split(',').Select(int.Parse).ToArray();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
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
        public void ReadList()
        {
            string hexBuffer = "82A168496E7456616C75650CA168496E7456616C75650D";
            List<IntObject> actualList = Helper.Read<List<IntObject>>(hexBuffer);

            Assert.IsNotNull(actualList);
            Assert.AreEqual(2, actualList.Count);
            Assert.IsNotNull(actualList[0]);
            Assert.AreEqual(12, actualList[0].IntValue);
            Assert.IsNotNull(actualList[1]);
            Assert.AreEqual(13, actualList[1].IntValue);
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
        public void ReadWithList()
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
        public void ReadWithObject()
        {
            const string hexBuffer = "A1664F626A656374A168496E7456616C75650C";
            ObjectWithObject obj = Helper.Read<ObjectWithObject>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.IsNotNull(obj.Object);
            Assert.AreEqual(12, obj.Object.IntValue);
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
        public void ReadWithCborProperty()
        {
            const string hexBuffer = "A16269640C";
            ObjectWithCborProperty obj = Helper.Read<ObjectWithCborProperty>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(12, obj.Id);
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
        public void ReadWithCustomConverterOnProperty()
        {
            const string hexBuffer = "A16447756964505DF4EB676C01484B8AE61E389127B717";
            ObjectWithCustomConverterOnProperty obj = Helper.Read<ObjectWithCustomConverterOnProperty>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(Guid.Parse("67EBF45D-016C-4B48-8AE6-1E389127B717"), obj.Guid);
        }

        [DataTestMethod]
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
