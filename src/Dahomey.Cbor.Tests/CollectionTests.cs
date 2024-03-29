﻿using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Tests
{

    public class CollectionTests
    {
        [Theory]
        [InlineData("8401020304", "1,2,3,4", null)]
        [InlineData("9F01020304FF", "1,2,3,4", null)]
        public void ReadInt32List(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            List<int> expectedList = expectedValue.Split(',').Select(int.Parse).ToList();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [Theory]
        [InlineData("8401020304", "1,2,3,4", null)]
        public void WriteInt32List(string hexBuffer, string value, Type expectedExceptionType)
        {
            List<int> list = value.Split(',').Select(int.Parse).ToList();
            Helper.TestWrite(list, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("A201010202", "1:1,2:2", null)]
        [InlineData("BF01010202FF", "1:1,2:2", null)]
        public void ReadInt32Dictionary(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            Dictionary<int, int> expectedDict = expectedValue
                .Split(',').Select(s => s.Split(':').Select(int.Parse).ToArray())
                .ToDictionary(i => i[0], i => i[1]);
            Helper.TestRead(hexBuffer, expectedDict, expectedExceptionType);
        }

        [Theory]
        [InlineData("A201010202", "1:1,2:2", null)]
        public void WriteInt32Dictionary(string hexBuffer, string value, Type expectedExceptionType)
        {
            Dictionary<int, int> dict = value
                .Split(',').Select(s => s.Split(':').Select(int.Parse).ToArray())
                .ToDictionary(i => i[0], i => i[1]);
            Helper.TestWrite(dict, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void ReadStringList(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            List<string> expectedList = expectedValue.Split(',').ToList();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [Theory]
        [InlineData("84626161626262626363626464", "aa,bb,cc,dd", null)]
        public void WriteStringList(string hexBuffer, string value, Type expectedExceptionType)
        {
            List<string> list = value.Split(',').ToList();
            Helper.TestWrite(list, hexBuffer, expectedExceptionType);
        }

        [Theory]
        [InlineData("8401020304", "1,2,3,4", null)]
        public void ReadInt32Array(string hexBuffer, string expectedValue, Type expectedExceptionType)
        {
            int[] expectedList = expectedValue.Split(',').Select(int.Parse).ToArray();
            Helper.TestRead(hexBuffer, expectedList, expectedExceptionType);
        }

        [Theory]
        [InlineData("8401020304", "1,2,3,4", null)]
        public void WriteInt32Array(string hexBuffer, string value, Type expectedExceptionType)
        {
            int[] array = value.Split(',').Select(int.Parse).ToArray();
            Helper.TestWrite(array, hexBuffer, expectedExceptionType);
        }

        [Fact]
        public void ReadObjectList()
        {
            string hexBuffer = "82A168496E7456616C75650CA168496E7456616C75650D";
            List<IntObject> actualList = Helper.Read<List<IntObject>>(hexBuffer);

            Assert.NotNull(actualList);
            Assert.Equal(2, actualList.Count);
            Assert.NotNull(actualList[0]);
            Assert.Equal(12, actualList[0].IntValue);
            Assert.NotNull(actualList[1]);
            Assert.Equal(13, actualList[1].IntValue);
        }

        [Fact]
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

        [Fact]
        public void ReadEmptyArray()
        {
            string hexBuffer = "80";
            int[] array = Helper.Read<int[]>(hexBuffer);

            Assert.Empty(array);
        }

        [Fact]
        public void WriteEmptyArray()
        {
            string hexBuffer = "80";
            int[] array = { };
            Helper.TestWrite(array, hexBuffer);
        }

        [Fact]
        public void ReadEmptyList()
        {
            string hexBuffer = "80";
            List<int> list = Helper.Read<List<int>>(hexBuffer);

            Assert.Empty(list);
        }

        [Fact]
        public void WriteEmptyList()
        {
            string hexBuffer = "80";
            List<int> list = new List<int>();
            Helper.TestWrite(list, hexBuffer);
        }

        [Fact]
        public void ReadEmptyDictionary()
        {
            string hexBuffer = "A0";
            Dictionary<int, int> dict = Helper.Read<Dictionary<int, int>>(hexBuffer);

            Assert.Empty(dict);
        }

        [Fact]
        public void WriteEmptyDictionary()
        {
            string hexBuffer = "A0";
            Dictionary<int, int> dict = new Dictionary<int, int>();
            Helper.TestWrite(dict, hexBuffer);
        }
    }
}
