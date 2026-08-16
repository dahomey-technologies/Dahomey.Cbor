using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A positional record: one constructor, no parameterless one, and `init`-only properties. Every
    /// part of that was a reason a generated context could not read it back.
    /// </summary>
    public record GeneratedRecord(int Id, string Name);

    /// <summary>A constructor named explicitly, which wins over any other.</summary>
    public class GeneratedCreatorHolder
    {
        [CborConstructor]
        public GeneratedCreatorHolder(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public GeneratedCreatorHolder()
        {
            Name = "unused";
        }

        public int Id { get; init; }

        public string Name { get; init; }
    }

    [CborSerializable(typeof(GeneratedRecord))]
    [CborSerializable(typeof(GeneratedCreatorHolder))]
    public partial class CreatorContext : CborSerializerContext
    {
    }

    /// <summary>
    /// A type built through a constructor rather than through <c>new T()</c> and a set of setters.
    /// </summary>
    /// <remarks>
    /// This is the gap that blocked records: a positional record has no parameterless constructor, so
    /// the generated factory could not build it, and its properties are <c>init</c>-only, so
    /// <c>(o, v) =&gt; o.X = v</c> does not compile for them either. Both stop mattering once the
    /// values arrive through the constructor.
    /// </remarks>
    public class GeneratedCreatorTests
    {
        [Fact]
        public void APositionalRecordRoundTripsThroughAGeneratedContext()
        {
            CreatorContext context = new CreatorContext();
            GeneratedRecord value = new GeneratedRecord(7, "seven");

            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value), generated, ignoreCase: true);

            GeneratedRecord read =
                Cbor.Deserialize<GeneratedRecord>(generated.HexToBytes(), context.Options);

            Assert.Equal(value, read);
        }

        /// <summary>
        /// <c>[CborConstructor]</c> wins over the parameterless constructor that is also declared, so
        /// the generated path picks the same one the reflection path does — which a round trip alone
        /// would not show, since both constructors can produce a correct object.
        /// </summary>
        [Fact]
        public void TheNamedConstructorIsUsedRatherThanTheParameterlessOne()
        {
            CreatorContext context = new CreatorContext();
            GeneratedCreatorHolder value = new GeneratedCreatorHolder(7, "seven");

            string generated = Helper.Write(value, context.Options);

            GeneratedCreatorHolder read =
                Cbor.Deserialize<GeneratedCreatorHolder>(generated.HexToBytes(), context.Options);

            // The parameterless constructor leaves Name at "unused"; only the named one carries it.
            Assert.Equal("seven", read.Name);
            Assert.Equal(7, read.Id);

            // And the reflection path agrees, which is what makes this the right constructor rather
            // than merely a working one.
            Assert.Equal("seven", Cbor.Deserialize<GeneratedCreatorHolder>(generated.HexToBytes()).Name);
        }

        internal static GeneratedRecord SampleRecord() => new GeneratedRecord(7, "seven");

        internal static GeneratedCreatorHolder SampleHolder() => new GeneratedCreatorHolder(7, "seven");
    }
}
