using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A type that declares <c>StringKeyMap</c> while its context's options say <c>Array</c> — the one
    /// combination where "the default" differs between the two paths.
    /// </summary>
    /// <remarks>
    /// An object mapping's own default object format is the *options* format, not <c>StringKeyMap</c>.
    /// So a generated context that only stated the format when it was not <c>StringKeyMap</c> left this
    /// type to inherit <c>Array</c> from the options, against its own attribute — and it failed as
    /// "expecting all fields/properties to get a member index", which names neither the attribute nor
    /// the option that overrode it. The reflection path calls <c>SetObjectFormat</c> whenever the
    /// attribute is present, so it was never affected.
    /// </remarks>
    [CborObjectFormat(CborObjectFormat.StringKeyMap)]
    public class GeneratedKeptMapFormat
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    /// <summary>A sibling taking the context's format, so both directions are covered by one context.</summary>
    public class GeneratedTakesArrayFormat
    {
        [CborProperty(0)]
        public int Id { get; set; }

        [CborProperty(1)]
        public string Name { get; set; }
    }

    [CborSerializable(typeof(GeneratedKeptMapFormat))]
    [CborSerializable(typeof(GeneratedTakesArrayFormat))]
    [CborSourceGenerationOptions(ObjectFormat = CborObjectFormat.Array)]
    public partial class ArrayDefaultContext : CborSerializerContext
    {
    }

    public class GeneratedObjectFormatTests
    {
        [Fact]
        public void ATypeDeclaringMapFormatKeepsItUnderAnArrayDefault()
        {
            ArrayDefaultContext context = new ArrayDefaultContext();
            GeneratedKeptMapFormat value = new GeneratedKeptMapFormat { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            // A2 -- a map of two, not an array. The attribute wins over the context's option.
            Assert.StartsWith("A2", generated, System.StringComparison.OrdinalIgnoreCase);

            // And it is what the reflection path writes for the same type under the same option, which
            // is the contract: the attribute wins there too.
            CborOptions reflection = new CborOptions { ObjectFormat = CborObjectFormat.Array };
            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);

            GeneratedKeptMapFormat read =
                Cbor.Deserialize<GeneratedKeptMapFormat>(generated.HexToBytes(), context.Options);

            Assert.Equal(7, read.Id);
            Assert.Equal("n", read.Name);
        }

        /// <summary>
        /// The other direction, so the fix cannot be "always emit StringKeyMap": a type with no
        /// attribute still takes the context's <c>Array</c>.
        /// </summary>
        [Fact]
        public void ATypeWithNoAttributeStillTakesTheContextFormat()
        {
            ArrayDefaultContext context = new ArrayDefaultContext();
            GeneratedTakesArrayFormat value = new GeneratedTakesArrayFormat { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            Assert.StartsWith("82", generated, System.StringComparison.OrdinalIgnoreCase);

            CborOptions reflection = new CborOptions { ObjectFormat = CborObjectFormat.Array };
            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);
        }
    }
}
