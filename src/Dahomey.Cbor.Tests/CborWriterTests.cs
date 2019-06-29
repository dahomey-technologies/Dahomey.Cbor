using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
            TestWrite(value, hexBuffer, expectedExceptionType);
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
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("18FF", byte.MaxValue, null)]
        [DataRow("0C", (byte)12, null)]
        [DataRow("2B", (byte)0, typeof(CborException))]
        [DataRow("00", (byte)0, null)]
        [DataRow("61", (byte)0, typeof(CborException))]
        public void WriteByte(string hexBuffer, byte value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
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
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("19FFFF", ushort.MaxValue, null)]
        [DataRow("0C", (ushort)12, null)]
        [DataRow("2B", (ushort)0, typeof(CborException))]
        [DataRow("00", (ushort)0, null)]
        [DataRow("61", (ushort)0, typeof(CborException))]
        public void WriteUInt16(string hexBuffer, ushort value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
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
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1AFFFFFFFF", uint.MaxValue, null)]
        [DataRow("0C", 12u, null)]
        [DataRow("2B", 0u, typeof(CborException))]
        [DataRow("00", 0u, null)]
        [DataRow("61", 0u, typeof(CborException))]
        public void WriteUInt32(string hexBuffer, uint value, Type expectedExceptionType)
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
            TestWrite(value, hexBuffer, expectedExceptionType);
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
        [DataRow("F6", null, null)]
        [DataRow("60", "", null)]
        public void WriteString(string hexBuffer, string value, Type expectedExceptionType)
        {
            TestWrite(value, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("1A4BFBAFFA", "2010-05-25T11:09:46Z", DateTimeFormat.Unix)]
        [DataRow("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z", DateTimeFormat.ISO8601)]
        public void WriteDateTime(string hexBuffer, string value, DateTimeFormat dateTimeFormat)
        {
            DateTime dateTime = DateTime.ParseExact(value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            TestWrite(dateTime, hexBuffer, null, new CborSerializationSettings { DateTimeFormat = dateTimeFormat });
        }

        [DataTestMethod]
        [DataRow("00", EnumTest.None, null, ValueFormat.WriteToInt)]
        [DataRow("01", EnumTest.Value1, null, ValueFormat.WriteToInt)]
        [DataRow("02", EnumTest.Value2, null, ValueFormat.WriteToInt)]
        [DataRow("03", (EnumTest)3, null, ValueFormat.WriteToInt)]
        [DataRow("644E6F6E65", EnumTest.None, null, ValueFormat.WriteToString)]
        [DataRow("6656616C756531", EnumTest.Value1, null, ValueFormat.WriteToString)]
        [DataRow("6656616C756532", EnumTest.Value2, null, ValueFormat.WriteToString)]
        [DataRow("04", (EnumTest)4, null, ValueFormat.WriteToString)]
        public void WriteEnum(string hexBuffer, EnumTest value, Type expectedExceptionType, ValueFormat enumFormat)
        {
            TestWrite(value, hexBuffer, expectedExceptionType, new CborSerializationSettings { EnumFormat = enumFormat });
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void WriteInt32List(string hexBuffer, string value, Type expectedExceptionType)
        {
            List<int> list = value.Split(',').Select(int.Parse).ToList();
            TestWrite(list, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void WriteStringList(string hexBuffer, string value, Type expectedExceptionType)
        {
            List<string> list = value.Split(',').ToList();
            TestWrite(list, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void WriteInt32Array(string hexBuffer, string value, Type expectedExceptionType)
        {
            int[] array = value.Split(',').Select(int.Parse).ToArray();
            TestWrite(array, hexBuffer, expectedExceptionType);
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
            TestWrite(obj, hexBuffer, null, new CborSerializationSettings { EnumFormat = ValueFormat.WriteToString });
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
            TestWrite(obj, hexBuffer, null, new CborSerializationSettings { EnumFormat = ValueFormat.WriteToString });
        }

        [TestMethod]
        public void WriteList()
        {
            const string hexBuffer = "82A168496E7456616C75650CA168496E7456616C75650D";

            List<IntObject> lst = new List<IntObject>
            {
                new IntObject { IntValue = 12 },
                new IntObject { IntValue = 13 }
            };

            TestWrite(lst, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithCborValue()
        {
            const string hexBuffer = "A16943626F7256616C75658301F5F6";

            CborValueObject obj = new CborValueObject
            {
                CborValue = new CborArray(1, true, null)
            };

            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithList()
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

            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithArray()
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

            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithDictionary()
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

            TestWrite(obj, hexBuffer, null, 
                new CborSerializationSettings { EnumFormat = ValueFormat.WriteToString });
        }

        [TestMethod]
        public void WriteObjectWithHashSet()
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

            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithObject()
        {
            const string hexBuffer = "A1664F626A656374A168496E7456616C75650C";

            ObjectWithObject obj = new ObjectWithObject
            {
                Object = new IntObject
                {
                    IntValue = 12
                }
            };

            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteGenericObject()
        {
            const string hexBuffer = "A16556616C756501";

            GenericObject<int> obj = new GenericObject<int>
            {
                Value = 1
            };

            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithCborProperty()
        {
            const string hexBuffer = "A16269640C";

            ObjectWithCborProperty obj = new ObjectWithCborProperty
            {
                Id = 12
            };

            TestWrite(obj, hexBuffer);
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
            TestWrite(obj, hexBuffer);
        }

        [TestMethod]
        public void WriteObjectWithCustomConverterOnProperty()
        {
            const string hexBuffer = "A16447756964505DF4EB676C01484B8AE61E389127B717";
            ObjectWithCustomConverterOnProperty obj = new ObjectWithCustomConverterOnProperty
            {
                Guid = Guid.Parse("67EBF45D-016C-4B48-8AE6-1E389127B717")
            };
            TestWrite(obj, hexBuffer);
        }

        #region Helpers

        private string Write<T>(T value, CborSerializationSettings settings = null)
        {
            ByteBufferWriter bufferWriter = new ByteBufferWriter();
            CborWriter writer = new CborWriter(bufferWriter, settings);
            ICborConverter<T> converter = CborConverter.Lookup<T>();
            converter.Write(ref writer, value);
            return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
        }

        private void TestWrite<T>(T value, string hexBuffer, Type expectedExceptionType = null, CborSerializationSettings settings = null)
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

        #endregion Helpers
    }
}
