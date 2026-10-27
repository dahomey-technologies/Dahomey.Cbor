using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
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

    /// <summary>
    /// A naming convention renames <c>FirstName</c> to <c>first_name</c>, so the unlisted parameter no
    /// longer matches its CBOR name. The reflection path leaves it at its default and lets the
    /// <c>init</c> accessor supply the value.
    /// </summary>
    public record GeneratedSnakeRecord(int FirstName, int Age);

    public record GeneratedKebabRecord(int FirstName, int Age);

    /// <summary>The same rename by attribute, under default options.</summary>
    public record GeneratedRenamedRecord([property: CborProperty("fn")] int FirstName, int Age);

    /// <summary>A settable member: reflection leaves the parameter at its default and runs the setter.</summary>
    public class GeneratedSnakeClass
    {
        public GeneratedSnakeClass(int firstName, int age)
        {
            FirstName = firstName;
            Age = age;
        }

        public int FirstName { get; set; }

        public int Age { get; set; }

        public override bool Equals(object obj) =>
            obj is GeneratedSnakeClass other && other.FirstName == FirstName && other.Age == Age;

        public override int GetHashCode() => FirstName * 31 + Age;
    }

    /// <summary>
    /// A constructor that transforms its arguments tells the two kinds of parameter apart: the one the
    /// document supplies (<c>age</c>) is transformed, the one left to its setter (<c>firstName</c>) is
    /// overwritten with the document's value untouched.
    /// </summary>
    public class GeneratedSnakeTransform
    {
        public GeneratedSnakeTransform(int firstName, int age)
        {
            FirstName = firstName * 10;
            Age = age * 10;
        }

        public int FirstName { get; set; }

        public int Age { get; set; }
    }

    /// <summary>Tuple element names are erased at run time, so these are one type.</summary>
    public class GeneratedTupleCreator
    {
        public GeneratedTupleCreator((int, int) p)
        {
            P = p;
        }

        public (int X, int Y) P { get; }

        public override bool Equals(object obj) => obj is GeneratedTupleCreator other && other.P.Equals(P);

        public override int GetHashCode() => P.GetHashCode();
    }

    /// <summary>
    /// A listed name is compared with the member without regard to case but with the document's key
    /// exactly, so <c>"a"</c> finds member <c>A</c> and then no value under <c>"a"</c>.
    /// </summary>
    public record GeneratedCaseLower
    {
        [CborConstructor("a", "B")]
        public GeneratedCaseLower(int a, int b)
        {
            A = a;
            B = b;
        }

        public int A { get; }

        public int B { get; set; }
    }

    public record GeneratedCaseUpper
    {
        [CborConstructor("ALPHA", "beta")]
        public GeneratedCaseUpper(int alpha, int beta)
        {
            Alpha = alpha;
            Beta = beta;
        }

        public int Alpha { get; }

        public int Beta { get; set; }
    }

    /// <summary>A struct whose sole constructor leaves <c>Y</c> unsupplied: read as default plus setters.</summary>
    public struct GeneratedPartialStruct
    {
        public int X;
        public int Y;

        public GeneratedPartialStruct(int x)
        {
            X = x;
            Y = 0;
        }
    }

    /// <summary>A struct whose constructor supplies every member is built through it.</summary>
    public struct GeneratedCompleteStruct
    {
        public GeneratedCompleteStruct(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    /// <summary>
    /// A type deriving from a base that declares the format: the object format is inherited, and
    /// the creator has to be told indexes, not names, for it. The base member is set by its setter.
    /// </summary>
    public class GeneratedInheritedArrayRecord : InheritedArrayBase
    {
        public GeneratedInheritedArrayRecord(string name)
        {
            Name = name;
        }

        [CborProperty(1)]
        public string Name { get; }

        public override bool Equals(object obj) =>
            obj is GeneratedInheritedArrayRecord other && other.Name == Name && other.Id == Id;

        public override int GetHashCode() => Id;
    }

    [CborObjectFormat(CborObjectFormat.IntKeyMap)]
    public class GeneratedIntKeyBase
    {
        [CborProperty(0)]
        public int Id { get; set; }
    }

    public class GeneratedInheritedIntKeyRecord : GeneratedIntKeyBase
    {
        public GeneratedInheritedIntKeyRecord(string name)
        {
            Name = name;
        }

        [CborProperty(4)]
        public string Name { get; }

        public override bool Equals(object obj) =>
            obj is GeneratedInheritedIntKeyRecord other && other.Name == Name && other.Id == Id;

        public override int GetHashCode() => Id;
    }

    [CborSerializable(typeof(GeneratedInheritedArrayRecord))]
    [CborSerializable(typeof(GeneratedInheritedIntKeyRecord))]
    public partial class CreatorInheritedContext : CborSerializerContext
    {
    }

    /// <summary>
    /// More parameters than <c>System.Func</c> has type arguments for (16 inputs and a result). The
    /// reflection path builds a delegate type for the lambda; a generated one has to declare its own.
    /// </summary>
    public record GeneratedWide17(
        int A01, string A02, long A03, int A04, int A05, int A06, int A07, int A08, int A09,
        int A10, int A11, int A12, int A13, int A14, int A15, int A16, string A17);

    public record GeneratedWide20(
        int A01, string A02, long A03, int A04, int A05, int A06, int A07, int A08, int A09, int A10,
        int A11, int A12, int A13, int A14, int A15, int A16, string A17, int A18, int A19, long A20);

    /// <summary>
    /// A required member satisfied by a creator marked [SetsRequiredMembers], beside a plain
    /// parameterless constructor that is not: the read goes through the creator, so no
    /// <c>new T()</c> is generated for it.
    /// </summary>
    public class GeneratedRequiredCreator
    {
        [CborConstructor]
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public GeneratedRequiredCreator(string name)
        {
            Name = name;
        }

        public GeneratedRequiredCreator()
        {
            Name = "unused";
        }

        public required string Name { get; set; }

        public override bool Equals(object obj) => obj is GeneratedRequiredCreator other && other.Name == Name;

        public override int GetHashCode() => Name?.GetHashCode() ?? 0;
    }

    public struct GeneratedRequiredStruct
    {
        [CborConstructor]
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public GeneratedRequiredStruct(int id)
        {
            Id = id;
        }

        public required int Id { get; set; }
    }

    /// <summary>A creator beside the two member-level features that decide what is written and read.</summary>
    public record GeneratedCreatorMemberFeatures([property: CborRequired] int Id, string Name)
    {
        public bool ShouldSerializeName() => Name != null;
    }

    [CborSerializable(typeof(GeneratedRecord))]
    [CborSerializable(typeof(GeneratedCreatorHolder))]
    [CborSerializable(typeof(GeneratedCreatorMemberFeatures))]
    [CborSerializable(typeof(GeneratedRequiredCreator))]
    [CborSerializable(typeof(GeneratedRequiredStruct))]
    [CborSerializable(typeof(GeneratedWide17))]
    [CborSerializable(typeof(GeneratedWide20))]
    [CborSerializable(typeof(GeneratedRenamedRecord))]
    [CborSerializable(typeof(GeneratedTupleCreator))]
    [CborSerializable(typeof(GeneratedCaseLower))]
    [CborSerializable(typeof(GeneratedCaseUpper))]
    [CborSerializable(typeof(GeneratedPartialStruct))]
    [CborSerializable(typeof(GeneratedCompleteStruct))]
    [CborSerializable(typeof(GeneratedListedNames))]
    [CborSerializable(typeof(GeneratedSealedRecord))]
    public partial class CreatorContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedSnakeRecord))]
    [CborSerializable(typeof(GeneratedSnakeClass))]
    [CborSerializable(typeof(GeneratedSnakeTransform))]
    [CborSourceGenerationOptions(NamingConvention = typeof(SnakeCaseNamingConvention))]
    public partial class CreatorSnakeContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedKebabRecord))]
    [CborSourceGenerationOptions(NamingConvention = typeof(KebabCaseNamingConvention))]
    public partial class CreatorKebabContext : CborSerializerContext
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
            return AssertMatchesReflection(context, new CborOptions { ObjectFormat = format }, value, document);
        }

        private static T AssertMatchesReflection<T>(
            CborSerializerContext context, CborOptions reflection, T value, string document)
        {
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

        // {"first_name": 1, "age": 2}
        private const string SnakeDocument = "A2" + "6A66697273745F6E616D65" + "01" + "63616765" + "02";

        [Fact]
        public void ARecordWhoseMemberTheConventionRenamedIsReadBack()
        {
            GeneratedSnakeRecord read = AssertMatchesReflection(
                new CreatorSnakeContext(),
                new CborOptions { DefaultNamingConvention = new SnakeCaseNamingConvention() },
                new GeneratedSnakeRecord(1, 2),
                SnakeDocument);

            Assert.Equal(new GeneratedSnakeRecord(1, 2), read);
        }

        [Fact]
        public void AKebabCaseRecordIsReadBack()
        {
            GeneratedKebabRecord read = AssertMatchesReflection(
                new CreatorKebabContext(),
                new CborOptions { DefaultNamingConvention = new KebabCaseNamingConvention() },
                new GeneratedKebabRecord(1, 2),
                "A2" + "6A66697273742D6E616D65" + "01" + "63616765" + "02");

            Assert.Equal(new GeneratedKebabRecord(1, 2), read);
        }

        [Fact]
        public void ARecordWhoseMemberAnAttributeRenamedIsReadBack()
        {
            // {"fn": 1, "Age": 2}
            GeneratedRenamedRecord read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedRenamedRecord(1, 2),
                "A2" + "62666E" + "01" + "63416765" + "02");

            Assert.Equal(new GeneratedRenamedRecord(1, 2), read);
        }

        [Fact]
        public void AClassWhoseSettableMemberTheConventionRenamedIsReadBack()
        {
            GeneratedSnakeClass read = AssertMatchesReflection(
                new CreatorSnakeContext(),
                new CborOptions { DefaultNamingConvention = new SnakeCaseNamingConvention() },
                new GeneratedSnakeClass(1, 2),
                SnakeDocument);

            Assert.Equal(new GeneratedSnakeClass(1, 2), read);
        }

        /// <summary>
        /// <c>FirstName</c> is left to its setter and keeps the document's 1; <c>Age</c> goes through
        /// the constructor and arrives as 20. The generated path has to agree on both, which a
        /// round-trip of plain assignments cannot show.
        /// </summary>
        [Fact]
        public void ASettableMemberTheConventionRenamedIsSetAfterConstruction()
        {
            CreatorSnakeContext context = new CreatorSnakeContext();
            CborOptions reflection = new CborOptions { DefaultNamingConvention = new SnakeCaseNamingConvention() };

            GeneratedSnakeTransform fromReflection =
                Cbor.Deserialize<GeneratedSnakeTransform>(SnakeDocument.HexToBytes(), reflection);
            GeneratedSnakeTransform fromGenerated =
                Cbor.Deserialize<GeneratedSnakeTransform>(SnakeDocument.HexToBytes(), context.Options);

            Assert.Equal(1, fromReflection.FirstName);
            Assert.Equal(20, fromReflection.Age);
            Assert.Equal(fromReflection.FirstName, fromGenerated.FirstName);
            Assert.Equal(fromReflection.Age, fromGenerated.Age);
        }

        [Fact]
        public void TupleElementNamesDoNotMakeAParameterAnotherType()
        {
            GeneratedTupleCreator read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedTupleCreator((1, 2)),
                Helper.Write(new GeneratedTupleCreator((1, 2))));

            Assert.Equal((1, 2), read.P);
        }

        /// <summary>
        /// Reflection finds member <c>A</c> for the listed <c>"a"</c> and then looks for a value under
        /// <c>"a"</c>, so <c>A</c> stays 0 although the document carries it. Passing the listed text on
        /// verbatim is what reproduces that; passing the member's own key would read 1.
        /// </summary>
        [Theory]
        [InlineData("A2" + "6141" + "01" + "6142" + "02", 0, 2)]
        public void AListedNameIsComparedWithTheDocumentKeyExactly(string document, int a, int b)
        {
            GeneratedCaseLower read = AssertMatchesReflection(
                new CreatorContext(), CborObjectFormat.StringKeyMap, new GeneratedCaseLower(1, 2), document);

            Assert.Equal(a, read.A);
            Assert.Equal(b, read.B);
        }

        [Fact]
        public void ListedNamesInTheWrongCaseFindNoValue()
        {
            // {"Alpha": 1, "Beta": 2}
            GeneratedCaseUpper read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedCaseUpper(1, 2),
                "A2" + "65416C706861" + "01" + "644265" + "7461" + "02");

            // Alpha has no setter and its listed key finds nothing. Beta's finds nothing either, but
            // its setter still supplies the document's 2 afterwards.
            Assert.Equal(0, read.Alpha);
            Assert.Equal(2, read.Beta);
        }

        /// <summary>
        /// Built through its constructor on master's behaviour: default plus the setters. The reflection
        /// path throws NotSupportedException for this struct, so there is nothing to compare against --
        /// the point is that the generated path keeps reading it.
        /// </summary>
        [Fact]
        public void AStructWhoseConstructorLeavesAMemberUnsuppliedStillReads()
        {
            GeneratedPartialStruct read = Cbor.Deserialize<GeneratedPartialStruct>(
                ("A2" + "6158" + "01" + "6159" + "02").HexToBytes(), new CreatorContext().Options);

            Assert.Equal(1, read.X);
            Assert.Equal(2, read.Y);
        }

        [Fact]
        public void AStructWhoseConstructorSuppliesEveryMemberIsBuiltThroughIt()
        {
            GeneratedCompleteStruct read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedCompleteStruct(1, 2),
                "A2" + "6158" + "01" + "6159" + "02");

            Assert.Equal(new GeneratedCompleteStruct(1, 2), read);
        }

        /// <summary>
        /// The format comes from the base class, not from the context, so the creator must be keyed by
        /// the format the type actually has -- an Array creator told names throws while the context
        /// is built.
        /// </summary>
        [Fact]
        public void ARecordDerivingFromAnArrayBaseIsCreatedByIndex()
        {
            GeneratedInheritedArrayRecord value = new GeneratedInheritedArrayRecord("n") { Id = 7 };

            GeneratedInheritedArrayRecord read = AssertMatchesReflection(
                new CreatorInheritedContext(),
                new CborOptions(),
                value,
                Helper.Write(value));

            Assert.Equal(value, read);
            Assert.Equal(7, read.Id);
        }

        [Fact]
        public void ARecordDerivingFromAnIntKeyMapBaseIsCreatedByIndex()
        {
            GeneratedInheritedIntKeyRecord value = new GeneratedInheritedIntKeyRecord("n") { Id = 7 };

            GeneratedInheritedIntKeyRecord read = AssertMatchesReflection(
                new CreatorInheritedContext(),
                new CborOptions(),
                value,
                Helper.Write(value));

            Assert.Equal(value, read);
            Assert.Equal(7, read.Id);
        }

        [Fact]
        public void AConstructorOfSeventeenParametersIsCalled()
        {
            GeneratedWide17 value = SampleWide17();

            Assert.Equal(value, AssertMatchesReflection(
                new CreatorContext(), CborObjectFormat.StringKeyMap, value, Helper.Write(value)));
        }

        [Fact]
        public void AConstructorOfTwentyParametersIsCalled()
        {
            GeneratedWide20 value = SampleWide20();

            Assert.Equal(value, AssertMatchesReflection(
                new CreatorContext(), CborObjectFormat.StringKeyMap, value, Helper.Write(value)));
        }

        internal static GeneratedWide17 SampleWide17() => new GeneratedWide17(
            1, "two", 3L, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, "seventeen");

        internal static GeneratedWide20 SampleWide20() => new GeneratedWide20(
            1, "two", 3L, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, "seventeen", 18, 19, 20L);

        [Fact]
        public void ARequiredMemberTheCreatorSatisfiesIsReadThroughTheCreator()
        {
            GeneratedRequiredCreator read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedRequiredCreator("n"),
                Helper.Write(new GeneratedRequiredCreator("n")));

            Assert.Equal("n", read.Name);
        }

        [Fact]
        public void ARequiredStructMemberTheCreatorSatisfiesIsReadThroughTheCreator()
        {
            GeneratedRequiredStruct read = AssertMatchesReflection(
                new CreatorContext(),
                CborObjectFormat.StringKeyMap,
                new GeneratedRequiredStruct(7),
                Helper.Write(new GeneratedRequiredStruct(7)));

            Assert.Equal(7, read.Id);
        }

        [Fact]
        public void ACreatorCoexistsWithShouldSerializeAndRequiredMembers()
        {
            CreatorContext context = new CreatorContext();

            // Written whole, and with Name omitted by ShouldSerializeName.
            foreach (GeneratedCreatorMemberFeatures value in new[]
            {
                new GeneratedCreatorMemberFeatures(7, "n"),
                new GeneratedCreatorMemberFeatures(7, null),
            })
            {
                Assert.Equal(value, AssertMatchesReflection(
                    context, CborObjectFormat.StringKeyMap, value, Helper.Write(value)));
            }

            // Id is required: a document without it ({"Name": "n"}) is refused by both paths alike.
            const string WithoutId = "A1" + "644E616D65" + "616E";

            Assert.Throws<CborException>(() => Cbor.Deserialize<GeneratedCreatorMemberFeatures>(
                WithoutId.HexToBytes(), context.Options));
            Assert.Throws<CborException>(() => Cbor.Deserialize<GeneratedCreatorMemberFeatures>(
                WithoutId.HexToBytes(), new CborOptions()));
        }

        internal static GeneratedRecord SampleRecord() => new GeneratedRecord(7, "seven");

        internal static GeneratedCreatorHolder SampleHolder() => new GeneratedCreatorHolder(7, "seven");
    }
}
