using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public struct BoxedPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class ObjectSlot
    {
        public object V { get; set; }
    }

    /// <summary>
    /// A struct in an <c>object</c> slot threw <see cref="NotSupportedException"/> on write (#265):
    /// the write runs on the converter for <c>object</c>, which hands the struct's members the boxed
    /// value, and their object-taking entry points were not implemented.
    /// </summary>
    public class BoxedStructWriteTests
    {
        [Fact]
        public void ABoxedStructIsWrittenAsItsOwnType()
        {
            AssertWrittenAsItself(new BoxedPoint { X = 1, Y = 2 });
            AssertWrittenAsItself(new KeyValuePair<string, int>("a", 1));
            AssertWrittenAsItself(TimeSpan.FromSeconds(90));
        }

        [Fact]
        public void ABoxedStructIsWrittenInAMemberAndACollection()
        {
            string point = Helper.Write(new BoxedPoint { X = 1, Y = 2 });

            Assert.Equal("A16156" + point, Helper.Write(new ObjectSlot { V = new BoxedPoint { X = 1, Y = 2 } }));
            Assert.Equal("81" + point, Helper.Write(new List<object> { new BoxedPoint { X = 1, Y = 2 } }));
        }

        private static void AssertWrittenAsItself<T>(T value)
        {
            Assert.Equal(Helper.Write(value), Helper.Write<object>(value));
        }
    }
}
