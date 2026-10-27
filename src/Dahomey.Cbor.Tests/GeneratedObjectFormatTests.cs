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

    /// <summary>A context with no source-generation options: the format comes from the options passed in.</summary>
    [CborSerializable(typeof(GeneratedTakesArrayFormat))]
    public partial class RuntimeOptionsContext : CborSerializerContext
    {
        public RuntimeOptionsContext() : this(new CborOptions { ObjectFormat = CborObjectFormat.Array })
        {
        }

        public RuntimeOptionsContext(CborOptions options) : base(options)
        {
        }
    }

    /// <summary>The decorated counterpart, in its own context so each case fails for its own reason.</summary>
    [CborSerializable(typeof(GeneratedKeptMapFormat))]
    public partial class RuntimeOptionsDecoratedContext : CborSerializerContext
    {
        public RuntimeOptionsDecoratedContext() : this(new CborOptions())
        {
        }

        public RuntimeOptionsDecoratedContext(CborOptions options) : base(options)
        {
        }
    }

    // [CborObjectFormat] is inherited on the reflection path (GetCustomAttribute<T>() walks the base
    // classes), so a subtype of a decorated base takes its format. The generated path has to agree.

    [CborObjectFormat(CborObjectFormat.Array)]
    public class InheritedArrayBase
    {
        [CborProperty(0)]
        public int Id { get; set; }
    }

    public class InheritedArrayDerived : InheritedArrayBase
    {
        [CborProperty(1)]
        public string Name { get; set; }
    }

    [CborObjectFormat(CborObjectFormat.Array)]
    public class InheritedGenericBase<T>
    {
        [CborProperty(0)]
        public T Value { get; set; }
    }

    public class InheritedGenericDerived : InheritedGenericBase<int>
    {
        [CborProperty(1)]
        public string Name { get; set; }
    }

    [CborObjectFormat(CborObjectFormat.Array)]
    [CborDiscriminator("base")]
    public class InheritedPolymorphicBase
    {
        [CborProperty(1)]
        public int Id { get; set; }
    }

    [CborDiscriminator("derived")]
    public class InheritedPolymorphicDerived : InheritedPolymorphicBase
    {
        [CborProperty(2)]
        public string Name { get; set; }
    }

    [CborObjectFormat(CborObjectFormat.StringKeyMap)]
    public class InheritedMapBase
    {
        public int Id { get; set; }
    }

    public class InheritedMapDerived : InheritedMapBase
    {
        public string Name { get; set; }
    }

    /// <summary>Nearest declaration wins: a subtype re-declaring the format overrides its base.</summary>
    [CborObjectFormat(CborObjectFormat.IntKeyMap)]
    public class InheritedOverrideDerived : InheritedArrayBase
    {
        [CborProperty(1)]
        public string Name { get; set; }
    }

    [CborSerializable(typeof(InheritedArrayDerived))]
    public partial class InheritedArrayContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(InheritedGenericDerived))]
    public partial class InheritedGenericContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(InheritedPolymorphicDerived))]
    public partial class InheritedPolymorphicContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(InheritedMapDerived))]
    [CborSourceGenerationOptions(ObjectFormat = CborObjectFormat.Array)]
    public partial class InheritedMapUnderArrayContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(InheritedOverrideDerived))]
    public partial class InheritedOverrideContext : CborSerializerContext
    {
    }

    public class GeneratedObjectFormatTests
    {
        /// <summary>
        /// An undecorated type must not be pinned to a format by the generated code: an ObjectFormat
        /// supplied through the options handed to the context applies to it, as on the reflection path.
        /// </summary>
        [Fact]
        public void AnUndecoratedTypeTakesTheFormatOfTheOptionsPassedToTheContext()
        {
            CborOptions supplied = new CborOptions { ObjectFormat = CborObjectFormat.Array };
            RuntimeOptionsContext context = new RuntimeOptionsContext(supplied);
            GeneratedTakesArrayFormat value = new GeneratedTakesArrayFormat { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            Assert.StartsWith("82", generated, System.StringComparison.OrdinalIgnoreCase);

            CborOptions reflection = new CborOptions { ObjectFormat = CborObjectFormat.Array };
            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);

            GeneratedTakesArrayFormat read =
                Cbor.Deserialize<GeneratedTakesArrayFormat>(generated.HexToBytes(), context.Options);
            Assert.Equal(7, read.Id);
            Assert.Equal("n", read.Name);
        }

        /// <summary>A decorated type still beats the options passed in, and matches reflection.</summary>
        [Fact]
        public void ADecoratedTypeKeepsItsFormatUnderTheOptionsPassedToTheContext()
        {
            RuntimeOptionsDecoratedContext context =
                new RuntimeOptionsDecoratedContext(new CborOptions { ObjectFormat = CborObjectFormat.Array });
            GeneratedKeptMapFormat value = new GeneratedKeptMapFormat { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            Assert.StartsWith("A2", generated, System.StringComparison.OrdinalIgnoreCase);

            CborOptions reflection = new CborOptions { ObjectFormat = CborObjectFormat.Array };
            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);
        }

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

        [Fact]
        public void ASubtypeInheritsItsBaseClassesArrayFormat()
        {
            InheritedArrayContext context = new InheritedArrayContext();
            InheritedArrayDerived value = new InheritedArrayDerived { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            // 82 07 61 6E -- an array of two, though only the base declares the format.
            Assert.Equal("8207616E", generated, ignoreCase: true);
            Assert.Equal(Helper.Write(value, new CborOptions()), generated, ignoreCase: true);

            InheritedArrayDerived read = Cbor.Deserialize<InheritedArrayDerived>(generated.HexToBytes(), context.Options);
            Assert.Equal(7, read.Id);
            Assert.Equal("n", read.Name);
        }

        [Fact]
        public void ASubtypeOfAGenericBaseInheritsItsFormat()
        {
            InheritedGenericContext context = new InheritedGenericContext();
            InheritedGenericDerived value = new InheritedGenericDerived { Value = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            Assert.StartsWith("82", generated, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Helper.Write(value, new CborOptions()), generated, ignoreCase: true);
        }

        [Fact]
        public void APolymorphicArrayBaseGivesItsSubtypeTheArrayFormat()
        {
            InheritedPolymorphicContext context = new InheritedPolymorphicContext();
            InheritedPolymorphicBase value = new InheritedPolymorphicDerived { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            // 83: an array of three -- the tagged discriminator, then the two members.
            Assert.StartsWith("83", generated, System.StringComparison.OrdinalIgnoreCase);

            CborOptions reflection = new CborOptions();
            reflection.Registry.DiscriminatorConventionRegistry.RegisterType<InheritedPolymorphicDerived>();
            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);
        }

        [Fact]
        public void ADeclaredMapFormatOnABaseSurvivesAnArrayDefaultInTheSubtype()
        {
            InheritedMapUnderArrayContext context = new InheritedMapUnderArrayContext();
            InheritedMapDerived value = new InheritedMapDerived { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            Assert.StartsWith("A2", generated, System.StringComparison.OrdinalIgnoreCase);

            CborOptions reflection = new CborOptions { ObjectFormat = CborObjectFormat.Array };
            Assert.Equal(Helper.Write(value, reflection), generated, ignoreCase: true);
        }

        [Fact]
        public void ASubtypeRedeclaringTheFormatOverridesItsBase()
        {
            InheritedOverrideContext context = new InheritedOverrideContext();
            InheritedOverrideDerived value = new InheritedOverrideDerived { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            // A2 00 07 01 61 6E -- an IntKeyMap, not the base's Array.
            Assert.Equal("A2000701616E", generated, ignoreCase: true);
            Assert.Equal(Helper.Write(value, new CborOptions()), generated, ignoreCase: true);
        }
    }
}
