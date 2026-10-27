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

    /// <summary>Positional records for the two positional object formats.</summary>
    public record GeneratedIndexedRecord(
        [property: CborProperty(0)] int Id,
        [property: CborProperty(1)] string Name);

    /// <summary>The same shape under a discriminator, which takes slot 0 in the Array format.</summary>
    [CborDiscriminator("indexed", Policy = CborDiscriminatorPolicy.Always)]
    public record GeneratedDiscriminatedRecord(
        [property: CborProperty(1)] int Id,
        [property: CborProperty(2)] string Name);

    /// <summary>Parameter order is the reverse of index order, so neither can stand in for the other.</summary>
    public record GeneratedSwappedRecord(
        [property: CborProperty(1)] int A,
        [property: CborProperty(0)] string B);

    /// <summary>Non-contiguous indexes, which only IntKeyMap allows -- Array reads by position.</summary>
    public record GeneratedSparseRecord(
        [property: CborProperty(5)] int A,
        [property: CborProperty(2)] string B);

    /// <summary>
    /// The arguments of <c>[CborConstructor]</c> decide which member fills which parameter, by
    /// position. Parameter <c>b</c> is filled from index 0, which is <c>A</c>: the names cross over, so
    /// matching by parameter name instead would give the opposite object. It still round-trips.
    /// </summary>
    public record GeneratedListedIndexes
    {
        [CborConstructor(0, 1)]
        public GeneratedListedIndexes(int b, int a)
        {
            A = b;
            B = a;
        }

        [CborProperty(0)]
        public int A { get; }

        [CborProperty(1)]
        public int B { get; }
    }

    public record GeneratedListedNames
    {
        [CborConstructor("A", "B")]
        public GeneratedListedNames(int b, int a)
        {
            A = b;
            B = a;
        }

        public int A { get; }

        public int B { get; }
    }

    /// <summary>A sealed record that declares its own copy constructor, privately.</summary>
    public sealed record GeneratedSealedRecord(int Id)
    {
        private GeneratedSealedRecord(GeneratedSealedRecord original)
        {
            Id = original.Id;
        }
    }

    [CborSerializable(typeof(GeneratedRecord))]
    [CborSerializable(typeof(GeneratedCreatorHolder))]
    [CborSerializable(typeof(GeneratedListedNames))]
    [CborSerializable(typeof(GeneratedSealedRecord))]
    public partial class CreatorContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedIndexedRecord))]
    [CborSerializable(typeof(GeneratedDiscriminatedRecord))]
    [CborSerializable(typeof(GeneratedSwappedRecord))]
    [CborSerializable(typeof(GeneratedListedIndexes))]
    [CborSourceGenerationOptions(ObjectFormat = CborObjectFormat.Array)]
    public partial class CreatorArrayContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedIndexedRecord))]
    [CborSerializable(typeof(GeneratedSwappedRecord))]
    [CborSerializable(typeof(GeneratedSparseRecord))]
    [CborSerializable(typeof(GeneratedListedIndexes))]
    [CborSourceGenerationOptions(ObjectFormat = CborObjectFormat.IntKeyMap)]
    public partial class CreatorIntKeyContext : CborSerializerContext
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

        private static void AssertIndexedRoundTrip(CborSerializerContext context, CborObjectFormat format)
        {
            GeneratedIndexedRecord value = new GeneratedIndexedRecord(7, "seven");
            CborOptions reflection = new CborOptions { ObjectFormat = format };

            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);
            Assert.Equal(value, Cbor.Deserialize<GeneratedIndexedRecord>(generated.HexToBytes(), context.Options));
            Assert.Equal(value, Cbor.Deserialize<GeneratedIndexedRecord>(generated.HexToBytes(), reflection));
        }

        [Fact]
        public void ARecordRoundTripsInTheArrayFormat()
        {
            AssertIndexedRoundTrip(new CreatorArrayContext(), CborObjectFormat.Array);
        }

        [Fact]
        public void ARecordRoundTripsInTheIntKeyMapFormat()
        {
            AssertIndexedRoundTrip(new CreatorIntKeyContext(), CborObjectFormat.IntKeyMap);
        }

        /// <summary>
        /// Writes <paramref name="value"/> through the context and through the reflection path under
        /// the same format, requires identical bytes, then reads <paramref name="document"/> through
        /// both and requires identical objects. Returns what the generated side read.
        /// </summary>
        private static T AssertMatchesReflection<T>(
            CborSerializerContext context, CborObjectFormat format, T value, string document)
        {
            CborOptions reflection = new CborOptions { ObjectFormat = format };

            Assert.Equal(
                Helper.Write(value, reflection), Helper.Write(value, context.Options), ignoreCase: true);

            T fromReflection = Cbor.Deserialize<T>(document.HexToBytes(), reflection);
            T fromGenerated = Cbor.Deserialize<T>(document.HexToBytes(), context.Options);

            Assert.Equal(fromReflection, fromGenerated);
            return fromGenerated;
        }

        /// <summary>
        /// Parameter order is not index order: <c>A</c> is the first parameter and index 1. A creator
        /// told positions 0..n-1, or the parameters in index order, reads the two swapped.
        /// </summary>
        [Theory]
        [InlineData(CborObjectFormat.Array, "82636F6E6501")]            // ["one", 1]
        [InlineData(CborObjectFormat.IntKeyMap, "A2006374776F0102")]
        public void ParameterOrderAndIndexOrderAreKeptApart(CborObjectFormat format, string document)
        {
            CborSerializerContext context = format == CborObjectFormat.Array
                ? new CreatorArrayContext()
                : new CreatorIntKeyContext();

            GeneratedSwappedRecord read = AssertMatchesReflection(
                context, format, new GeneratedSwappedRecord(1, "one"), document);

            // Index 0 is B, index 1 is A -- in both documents.
            Assert.Equal(format == CborObjectFormat.Array ? "one" : "two", read.B);
            Assert.Equal(format == CborObjectFormat.Array ? 1 : 2, read.A);
        }

        /// <summary>
        /// Declared indexes 5 and 2: positions would be 0 and 1. Only a creator given the declared
        /// indexes finds either value.
        /// </summary>
        [Fact]
        public void NonContiguousIntKeysAreTheDeclaredOnes()
        {
            GeneratedSparseRecord read = AssertMatchesReflection(
                new CreatorIntKeyContext(),
                CborObjectFormat.IntKeyMap,
                new GeneratedSparseRecord(7, "seven"),
                "A2" + "02" + "65736576656E" + "05" + "07");

            Assert.Equal(new GeneratedSparseRecord(7, "seven"), read);
        }

        /// <summary>
        /// A discriminator takes slot 0 of an Array, so the declared indexes start at 1. With
        /// <c>Always</c> it is on the wire (<c>83 D827 67 ...</c>), which is the only way this test
        /// sees the creator being told the declared indexes rather than counting from the first item.
        /// </summary>
        [Fact]
        public void ADiscriminatedRecordKeepsItsDeclaredIndexes()
        {
            CreatorArrayContext context = new CreatorArrayContext();
            GeneratedDiscriminatedRecord value = new GeneratedDiscriminatedRecord(7, "seven");

            string generated = Helper.Write(value, context.Options);

            Assert.StartsWith("83D82767696E6465786564", generated, System.StringComparison.OrdinalIgnoreCase);

            GeneratedDiscriminatedRecord read = AssertMatchesReflection(
                context, CborObjectFormat.Array, value, generated);

            Assert.Equal(value, read);
        }

        /// <summary>
        /// <c>[CborConstructor(0, 1)]</c> says parameter <c>b</c> comes from index 0 and <c>a</c> from
        /// index 1. Matching by parameter name instead gives the opposite object.
        /// </summary>
        [Theory]
        [InlineData(CborObjectFormat.Array, "820201")]
        [InlineData(CborObjectFormat.IntKeyMap, "A200020101")]
        public void TheIndexesListedOnTheConstructorAreHonoured(CborObjectFormat format, string document)
        {
            CborSerializerContext context = format == CborObjectFormat.Array
                ? new CreatorArrayContext()
                : new CreatorIntKeyContext();

            GeneratedListedIndexes read = AssertMatchesReflection(
                context, format, new GeneratedListedIndexes(2, 1), document);

            // b <- 2 (first listed key), a <- 1 -- by name it would be b <- 1, a <- 2.
            Assert.Equal(2, read.A);
            Assert.Equal(1, read.B);
        }

        [Fact]
        public void TheNamesListedOnTheConstructorAreHonoured()
        {
            // {"A": 2, "B": 1}
            GeneratedListedNames read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedListedNames(2, 1),
                "A2614102614201");

            // b <- 2 (first listed key), a <- 1 -- by name it would be b <- 1, a <- 2.
            Assert.Equal(2, read.A);
            Assert.Equal(1, read.B);
        }

        [Fact]
        public void ASealedRecordWithAPrivateCopyConstructorIsBuiltThroughItsPrimaryOne()
        {
            CreatorContext context = new CreatorContext();
            GeneratedSealedRecord read = AssertMatchesReflection(
                context, CborObjectFormat.StringKeyMap, new GeneratedSealedRecord(7), "A1624964" + "07");

            Assert.Equal(7, read.Id);
        }

        internal static GeneratedRecord SampleRecord() => new GeneratedRecord(7, "seven");

        internal static GeneratedCreatorHolder SampleHolder() => new GeneratedCreatorHolder(7, "seven");
    }
}
