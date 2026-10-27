using System.Collections.Immutable;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class ImmutableArrayHolder
    {
        public ImmutableArray<int> A { get; set; }
    }

    /// <summary>
    /// A default <see cref="ImmutableArray{T}"/>, which a member holds until it is assigned, threw
    /// <see cref="System.InvalidOperationException"/> on write (#265). It is written as null, and null
    /// reads back as the default.
    /// </summary>
    public class DefaultImmutableArrayTests
    {
        [Fact]
        public void ADefaultArrayIsWrittenAsNull()
        {
            Assert.Equal("F6", Helper.Write(default(ImmutableArray<int>)));
            Assert.Equal("A16141F6", Helper.Write(new ImmutableArrayHolder()));
            Assert.Equal("80", Helper.Write(ImmutableArray<int>.Empty));
        }

        [Fact]
        public void NullIsReadAsTheDefault()
        {
            Assert.True(Cbor.Deserialize<ImmutableArray<int>>("F6".HexToBytes()).IsDefault);
            Assert.True(Cbor.Deserialize<ImmutableArrayHolder>("A16141F6".HexToBytes()).A.IsDefault);
        }
    }
}
