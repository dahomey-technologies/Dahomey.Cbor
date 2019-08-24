using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CollectionTests
    {
        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        [DataRow("9F01020304FF", "1,2,3,4", null)]
        public void ReadInt32List(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            List<int> expectedList = expectedValue.Split(',').Select(int.Parse).ToList();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void WriteInt32List(string hexBuffer, string value, Type expectedExceptionType)
        {
            List<int> list = value.Split(',').Select(int.Parse).ToList();
            Helper.TestWrite(list, hexBuffer, expectedExceptionType);
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
        [DataRow("A201010202", "1:1,2:2", null)]
        public void WriteInt32Dictionary(string hexBuffer, string value, Type expectedExceptionType)
        {
            Dictionary<int, int> dict = value
                .Split(',').Select(s => s.Split(':').Select(int.Parse).ToArray())
                .ToDictionary(i => i[0], i => i[1]);
            Helper.TestWrite(dict, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void ReadStringList(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            List<string> expectedList = expectedValue.Split(',').ToList();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void WriteStringList(string hexBuffer, string value, Type expectedExceptionType)
        {
            List<string> list = value.Split(',').ToList();
            Helper.TestWrite(list, hexBuffer, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void ReadInt32Array(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            int[] expectedList = expectedValue.Split(',').Select(int.Parse).ToArray();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("8401020304", "1,2,3,4", null)]
        public void WriteInt32Array(string hexBuffer, string value, Type expectedExceptionType)
        {
            int[] array = value.Split(',').Select(int.Parse).ToArray();
            Helper.TestWrite(array, hexBuffer, expectedExceptionType);
        }

        [TestMethod]
        public void ReadObjectList()
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

        [TestMethod]
        public void WriteObjectList()
        {
            const string hexBuffer = "82A168496E7456616C75650CA168496E7456616C75650D";

            List<IntObject> lst = new List<IntObject>
            {
                new IntObject { IntValue = 12 },
                new IntObject { IntValue = 13 }
            };

            Helper.TestWrite(lst, hexBuffer);
        }
    }
}
