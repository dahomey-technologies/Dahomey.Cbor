using System;
using System.Collections.Generic;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A struct has no null, so null read into one is refused like any other item of the wrong type,
    /// as int or Guid refuse it, rather than read as default.
    /// </summary>
    public class NullIntoStructTests
    {
        public struct Point
        {
            public int X { get; set; }
        }

        public class Holder
        {
            public Point P { get; set; }
            public Point? Q { get; set; }
        }

        [Fact]
        public void NullIsRefusedAtTheRoot()
        {
            Assert.Equal("$", Assert.Throws<CborException>(() => Cbor.Deserialize<Point>("F6".HexToBytes())).Path);
            Assert.Throws<CborException>(() => Cbor.Deserialize<KeyValuePair<int, int>>("F6".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<TimeSpan>("F6".HexToBytes()));
        }

        [Fact]
        public void NullIsRefusedAsAMember()
        {
            Assert.Equal("$.P", Assert.Throws<CborException>(() => Cbor.Deserialize<Holder>("A16150F6".HexToBytes())).Path);
        }

        [Fact]
        public void ANullableStructAndAClassStillReadNull()
        {
            Assert.Null(Cbor.Deserialize<Point?>("F6".HexToBytes()));
            Assert.Null(Cbor.Deserialize<Holder>("F6".HexToBytes()));
            Assert.Null(Cbor.Deserialize<Holder>("A16151F6".HexToBytes()).Q);
        }
    }
}
