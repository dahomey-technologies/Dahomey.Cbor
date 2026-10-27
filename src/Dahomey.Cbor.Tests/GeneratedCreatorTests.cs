using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    // A type built through a constructor rather than through new T() and its setters: a positional
    // record has no parameterless constructor and init-only members, so a generated context could
    // neither create one nor populate it. Each case reads the same document through the reflection
    // path and the context and requires the same object, after requiring the same bytes.

    public record GeneratedRecord(int Id, string Name);

    /// <summary>[CborConstructor] wins over the parameterless constructor that is also declared.</summary>
    public record GeneratedCreatorHolder
    {
        [CborConstructor]
        public GeneratedCreatorHolder(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public GeneratedCreatorHolder() => Name = "unused";

        public int Id { get; init; }

        public string Name { get; init; }
    }

    /// <summary>Past the sixteen inputs System.Func has; the context declares a delegate type instead.</summary>
    public record GeneratedWide(
        int A01, string A02, long A03, int A04, int A05, int A06, int A07, int A08, int A09,
        int A10, int A11, int A12, int A13, int A14, int A15, int A16, string A17);

    /// <summary>A parameter is matched on the member's C# name, so a convention renaming it is no obstacle.</summary>
    public record GeneratedSnakeGetOnly
    {
        public GeneratedSnakeGetOnly(int firstName, string lastName)
        {
            FirstName = firstName;
            LastName = lastName;
        }

        public int FirstName { get; }

        public string LastName { get; }
    }

    /// <summary>Names listed on the constructor cross the parameters over; "a" finds member A ignoring case.</summary>
    public record GeneratedListedNames
    {
        [CborConstructor("B", "a")]
        public GeneratedListedNames(int a, int b)
        {
            A = b;
            B = a;
        }

        public int A { get; }

        public int B { get; }
    }

    /// <summary>Indexes listed on the constructor cross the parameters over.</summary>
    public record GeneratedListedIndexes
    {
        [CborConstructor(1, 0)]
        public GeneratedListedIndexes(int a, int b)
        {
            A = b;
            B = a;
        }

        [CborProperty(0)]
        public int A { get; }

        [CborProperty(1)]
        public int B { get; }
    }

    /// <summary>Parameter order is the reverse of index order.</summary>
    public record GeneratedSwapped([property: CborProperty(1)] int A, [property: CborProperty(0)] string B);

    /// <summary>Non-contiguous indexes, which only IntKeyMap allows.</summary>
    public record GeneratedSparse([property: CborProperty(5)] int A, [property: CborProperty(2)] string B);

    /// <summary>A discriminator takes slot 0 of an Array, so the declared indexes start at 1.</summary>
    [CborDiscriminator("discriminated", Policy = CborDiscriminatorPolicy.Always)]
    public record GeneratedDiscriminated([property: CborProperty(1)] int Id, [property: CborProperty(2)] string Name);

    /// <summary>The format comes from the base class, not from the context.</summary>
    public class GeneratedInheritedArrayCreator : InheritedArrayBase
    {
        public GeneratedInheritedArrayCreator(string name) => Name = name;

        [CborProperty(1)]
        public string Name { get; }

        public override bool Equals(object obj) =>
            obj is GeneratedInheritedArrayCreator other && other.Name == Name && other.Id == Id;

        public override int GetHashCode() => Id;
    }

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

    /// <summary>The constructor leaves Y unsupplied, so the struct is read as default plus setters.</summary>
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

    /// <summary>The creator sets the required member, the plain constructor does not: no new T() is generated.</summary>
    public class GeneratedRequiredCreator
    {
        [CborConstructor]
        [SetsRequiredMembers]
        public GeneratedRequiredCreator(string name) => Name = name;

        public GeneratedRequiredCreator() => Name = "unused";

        public required string Name { get; set; }

        public override bool Equals(object obj) => obj is GeneratedRequiredCreator other && other.Name == Name;

        public override int GetHashCode() => Name?.GetHashCode() ?? 0;
    }

    [CborSerializable(typeof(GeneratedRecord))]
    [CborSerializable(typeof(GeneratedCreatorHolder))]
    [CborSerializable(typeof(GeneratedWide))]
    [CborSerializable(typeof(GeneratedListedNames))]
    [CborSerializable(typeof(GeneratedCompleteStruct))]
    [CborSerializable(typeof(GeneratedPartialStruct))]
    [CborSerializable(typeof(GeneratedRequiredCreator))]
    public partial class CreatorContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedSnakeGetOnly))]
    [CborSourceGenerationOptions(NamingConvention = typeof(SnakeCaseNamingConvention))]
    public partial class CreatorSnakeContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedSwapped))]
    [CborSerializable(typeof(GeneratedDiscriminated))]
    [CborSerializable(typeof(GeneratedListedIndexes))]
    [CborSerializable(typeof(GeneratedInheritedArrayCreator))]
    [CborSourceGenerationOptions(ObjectFormat = CborObjectFormat.Array)]
    public partial class CreatorArrayContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedSwapped))]
    [CborSerializable(typeof(GeneratedSparse))]
    [CborSerializable(typeof(GeneratedListedIndexes))]
    [CborSourceGenerationOptions(ObjectFormat = CborObjectFormat.IntKeyMap)]
    public partial class CreatorIntKeyContext : CborSerializerContext
    {
    }

    public class GeneratedCreatorTests
    {
        internal static readonly Dictionary<Type, Func<object>> Samples = new Dictionary<Type, Func<object>>
        {
            [typeof(GeneratedRecord)] = () => new GeneratedRecord(7, "seven"),
            [typeof(GeneratedCreatorHolder)] = () => new GeneratedCreatorHolder(7, "seven"),
            [typeof(GeneratedWide)] = () => new GeneratedWide(1, "two", 3L, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, "seventeen"),
            [typeof(GeneratedSnakeGetOnly)] = () => new GeneratedSnakeGetOnly(1, "one"),
            [typeof(GeneratedListedNames)] = () => new GeneratedListedNames(1, 2),
            [typeof(GeneratedListedIndexes)] = () => new GeneratedListedIndexes(1, 2),
            [typeof(GeneratedSwapped)] = () => new GeneratedSwapped(1, "one"),
            [typeof(GeneratedSparse)] = () => new GeneratedSparse(7, "seven"),
            [typeof(GeneratedDiscriminated)] = () => new GeneratedDiscriminated(7, "seven"),
            [typeof(GeneratedInheritedArrayCreator)] = () => new GeneratedInheritedArrayCreator("n") { Id = 7 },
            [typeof(GeneratedCompleteStruct)] = () => new GeneratedCompleteStruct(1, 2),
            [typeof(GeneratedPartialStruct)] = () => new GeneratedPartialStruct { X = 1, Y = 2 },
            [typeof(GeneratedRequiredCreator)] = () => new GeneratedRequiredCreator("n"),
        };

        private static CborOptions Reflection(CborObjectFormat format = CborObjectFormat.StringKeyMap) =>
            new CborOptions { ObjectFormat = format };

        /// <summary>
        /// Requires identical bytes for <paramref name="value"/>, then reads <paramref name="document"/>
        /// (the value's own encoding unless given) through both and requires identical objects.
        /// </summary>
        private static T Compare<T>(CborSerializerContext context, CborOptions reflection, T value, string document = null)
        {
            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);

            byte[] bytes = (document ?? generated).HexToBytes();
            T fromReflection = Cbor.Deserialize<T>(bytes, reflection);
            T fromGenerated = Cbor.Deserialize<T>(bytes, context.Options);

            Assert.Equal(fromReflection, fromGenerated);
            return fromGenerated;
        }

        [Fact]
        public void APositionalRecordIsRead()
        {
            Assert.Equal(new GeneratedRecord(7, "seven"),
                Compare(new CreatorContext(), Reflection(), new GeneratedRecord(7, "seven")));
        }

        [Fact]
        public void AMarkedConstructorWinsOverTheParameterlessOne()
        {
            Assert.Equal("seven",
                Compare(new CreatorContext(), Reflection(), new GeneratedCreatorHolder(7, "seven")).Name);
        }

        [Fact]
        public void AConstructorBeyondSixteenParametersIsCalled()
        {
            GeneratedWide value = (GeneratedWide)Samples[typeof(GeneratedWide)]();

            Assert.Equal(value, Compare(new CreatorContext(), Reflection(), value));
        }

        [Fact]
        public void AConventionRenamingAMemberDoesNotHideItFromItsParameter()
        {
            // {"first_name": 1, "last_name": "one"}: neither member has a setter, so the constructor
            // is the only way either value arrives.
            GeneratedSnakeGetOnly read = Compare(
                new CreatorSnakeContext(),
                new CborOptions { DefaultNamingConvention = new SnakeCaseNamingConvention() },
                new GeneratedSnakeGetOnly(1, "one"));

            Assert.Equal(1, read.FirstName);
            Assert.Equal("one", read.LastName);
        }

        [Fact]
        public void NamesListedOnTheConstructorKeyTheParametersByThePositionTheyAreListedAt()
        {
            // {"A": 1, "B": 2}: a <- "B", b <- "A" (listed as "a", normalised to the member's key)
            GeneratedListedNames read = Compare(
                new CreatorContext(), Reflection(), new GeneratedListedNames(1, 2), "A2" + "6141" + "01" + "6142" + "02");

            Assert.Equal(1, read.A);
            Assert.Equal(2, read.B);
        }

        [Theory]
        [InlineData(CborObjectFormat.Array, "82" + "02" + "01")]
        [InlineData(CborObjectFormat.IntKeyMap, "A2" + "00" + "02" + "01" + "01")]
        public void IndexesListedOnTheConstructorKeyTheParametersByThePositionTheyAreListedAt(CborObjectFormat format, string document)
        {
            // index 0 = 2, index 1 = 1: a <- index 1, b <- index 0
            GeneratedListedIndexes read = Compare(
                format == CborObjectFormat.Array ? new CreatorArrayContext() : (CborSerializerContext)new CreatorIntKeyContext(),
                Reflection(format), new GeneratedListedIndexes(1, 2), document);

            Assert.Equal(2, read.A);
            Assert.Equal(1, read.B);
        }

        [Theory]
        [InlineData(CborObjectFormat.Array)]
        [InlineData(CborObjectFormat.IntKeyMap)]
        public void ParameterOrderIsNotIndexOrder(CborObjectFormat format)
        {
            GeneratedSwapped value = new GeneratedSwapped(1, "one");

            Assert.Equal(value, Compare(
                format == CborObjectFormat.Array ? new CreatorArrayContext() : (CborSerializerContext)new CreatorIntKeyContext(),
                Reflection(format), value));
        }

        [Fact]
        public void NonContiguousIndexesAreTheDeclaredOnes()
        {
            GeneratedSparse value = new GeneratedSparse(7, "seven");

            Assert.Equal(value, Compare(new CreatorIntKeyContext(), Reflection(CborObjectFormat.IntKeyMap), value));
        }

        [Fact]
        public void ADiscriminatorKeepsTheDeclaredIndexes()
        {
            GeneratedDiscriminated value = new GeneratedDiscriminated(7, "seven");

            // 83 D827 6D "discriminated" ...: the discriminator is on the wire, in slot 0.
            Assert.StartsWith("83D8276D", Helper.Write(value, new CreatorArrayContext().Options), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(value, Compare(new CreatorArrayContext(), Reflection(CborObjectFormat.Array), value));
        }

        [Fact]
        public void AnInheritedObjectFormatDecidesHowTheCreatorIsKeyed()
        {
            GeneratedInheritedArrayCreator value = new GeneratedInheritedArrayCreator("n") { Id = 7 };

            Assert.Equal(value, Compare(new CreatorArrayContext(), new CborOptions(), value));
        }

        [Fact]
        public void AStructTheConstructorFillsCompletelyIsBuiltThroughIt()
        {
            Assert.Equal(new GeneratedCompleteStruct(1, 2),
                Compare(new CreatorContext(), Reflection(), new GeneratedCompleteStruct(1, 2)));
        }

        /// <summary>The reflection path throws for this struct; the generated path keeps reading it as it always did.</summary>
        [Fact]
        public void AStructTheConstructorLeavesPartlyUnfilledIsReadByItsSetters()
        {
            GeneratedPartialStruct read = Cbor.Deserialize<GeneratedPartialStruct>(
                ("A2" + "6158" + "01" + "6159" + "02").HexToBytes(), new CreatorContext().Options);

            Assert.Equal(1, read.X);
            Assert.Equal(2, read.Y);
        }

        [Fact]
        public void ARequiredMemberACreatorSetsIsRead()
        {
            Assert.Equal("n", Compare(new CreatorContext(), Reflection(), new GeneratedRequiredCreator("n")).Name);
        }
    }
}
