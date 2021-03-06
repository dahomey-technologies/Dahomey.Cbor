﻿using Xunit;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Dahomey.Cbor.Tests
{

    public class ImmutableCollectionsTests
    {
        [Fact]
        public void WriteImmutableArrayOfInt32()
        {
            ImmutableArray<int> value = ImmutableArray.CreateRange(new[] { 1, 2, 3 });
            const string expectedHexBuffer = "83010203";
            Helper.TestWrite(value, expectedHexBuffer);
        }

        [Fact]
        public void ReadImmutableArrayOfInt32()
        {
            ImmutableArray<int> expectedValue = ImmutableArray.CreateRange(new[] { 1, 2, 3 });
            const string hexBuffer = "83010203";
            Helper.TestRead(hexBuffer, expectedValue);
        }

        [Fact]
        public void WriteImmutableListOfInt32()
        {
            ImmutableList<int> value = ImmutableList.CreateRange(new[] { 1, 2, 3 });
            const string expectedHexBuffer = "83010203";
            Helper.TestWrite(value, expectedHexBuffer);
        }

        [Fact]
        public void ReadImmutableListOfInt32()
        {
            ImmutableList<int> expectedValue = ImmutableList.CreateRange(new[] { 1, 2, 3 });
            const string hexBuffer = "83010203";
            Helper.TestRead(hexBuffer, expectedValue);
        }

        [Fact]
        public void WriteImmutableHashSetOfInt32()
        {
            ImmutableHashSet<int> value = ImmutableHashSet.CreateRange(new[] { 1, 2, 3 });
            const string expectedHexBuffer = "83010203";
            Helper.TestWrite(value, expectedHexBuffer);
        }

        [Fact]
        public void ReadImmutableHashSetOfInt32()
        {
            ImmutableHashSet<int> expectedValue = ImmutableHashSet.CreateRange(new[] { 1, 2, 3 });
            const string hexBuffer = "83010203";
            Helper.TestRead(hexBuffer, expectedValue);
        }

        [Fact]
        public void WriteImmutableSortedSetOfInt32()
        {
            ImmutableSortedSet<int> value = ImmutableSortedSet.CreateRange(new[] { 1, 2, 3 });
            const string expectedHexBuffer = "83010203";
            Helper.TestWrite(value, expectedHexBuffer);
        }

        [Fact]
        public void ReadImmutableSortedSetOfInt32()
        {
            ImmutableSortedSet<int> expectedValue = ImmutableSortedSet.CreateRange(new[] { 1, 2, 3 });
            const string hexBuffer = "83010203";
            Helper.TestRead(hexBuffer, expectedValue);
        }

        [Fact]
        public void WriteImmutableDictionaryOfInt32()
        {
            ImmutableDictionary<int, int> value = ImmutableDictionary.CreateRange(new Dictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 }
            });
            const string expectedHexBuffer = "A3010102020303";
            Helper.TestWrite(value, expectedHexBuffer);
        }

        [Fact]
        public void ReadImmutableDictionaryOfInt32()
        {
            ImmutableDictionary<int, int> expectedValue = ImmutableDictionary.CreateRange(new Dictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 }
            });
            const string hexBuffer = "A3010102020303";
            Helper.TestRead(hexBuffer, expectedValue);
        }

        [Fact]
        public void WriteImmutableSortedDictionaryOfInt32()
        {
            ImmutableSortedDictionary<int, int> value = ImmutableSortedDictionary.CreateRange(new SortedDictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 }
            });
            const string expectedHexBuffer = "A3010102020303";
            Helper.TestWrite(value, expectedHexBuffer);
        }

        [Fact]
        public void ReadImmutableSortedDictionaryOfInt32()
        {
            ImmutableSortedDictionary<int, int> expectedValue = ImmutableSortedDictionary.CreateRange(new SortedDictionary<int, int>
            {
                { 1, 1 },
                { 2, 2 },
                { 3, 3 }
            });
            const string hexBuffer = "A3010102020303";
            Helper.TestRead(hexBuffer, expectedValue);
        }
    }
}
