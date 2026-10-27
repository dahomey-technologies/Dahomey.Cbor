using Dahomey.Cbor.ObjectModel;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class CborStringHolder
    {
        public CborString S { get; set; }
    }

    /// <summary>
    /// A concrete object-model type used as the target of a read, or as a member type, threw
    /// <see cref="InvalidCastException"/>; a node asked for a value it cannot convert to threw
    /// <see cref="TypeInitializationException"/> (#265).
    /// </summary>
    public class CborValueSubtypeTests
    {
        [Fact]
        public void AConcreteTypeIsRead()
        {
            Assert.Equal("a", Helper.Read<CborString>("6161").Value<string>());
            Assert.Equal(5ul, Helper.Read<CborPositive>("05").Value<ulong>());
            Assert.Same(CborValue.Null, Helper.Read<CborNull>("F6"));
            Assert.Null(Helper.Read<CborString>("F6"));
        }

        [Fact]
        public void AnotherKindOfItemIsRefused()
        {
            CborException exception = Assert.Throws<CborException>(() => Helper.Read<CborString>("05"));
            Assert.Contains("Expected a CborString, got a CborPositive", exception.Message);
        }

        [Fact]
        public void AConcreteTypeMemberRoundTrips()
        {
            Assert.Equal("A1615363616161", Helper.Write(new CborStringHolder { S = "aaa" }));
            Assert.Equal("aaa", Helper.Read<CborStringHolder>("A1615363616161").S.Value<string>());
            Assert.Equal("6161", Helper.Write((CborString)"a"));
        }

        [Fact]
        public void AnUnsupportedConversionThrowsNotSupported()
        {
            CborValue number = 5;
            CborValue text = "a";

            // Twice: the first failure used to poison the type initializer for that pair.
            for (int i = 0; i < 2; i++)
            {
                Assert.Throws<NotSupportedException>(() => number.Value<bool>());
                Assert.Throws<NotSupportedException>(() => text.Value<int>());
                Assert.Throws<NotSupportedException>(() => number.Value<Guid>());
            }
        }
    }
}
