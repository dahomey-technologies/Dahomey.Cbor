using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dahomey.Cbor.GeneratorTests
{
    /// <summary>
    /// The generator's contract is that anything it cannot reproduce is a build error rather than a
    /// silent divergence from the reflection path. These are the cases where it used to say nothing.
    /// </summary>
    public class DiagnosticTests
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
";

        private static IReadOnlyList<Diagnostic> Run(string body)
        {
            return GeneratorHarness.Run(Preamble + body);
        }

        private static void AssertReports(string id, string body)
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(body);

            Assert.True(
                diagnostics.Any(diagnostic => diagnostic.Id == id),
                $"expected {id}, got: {(diagnostics.Count == 0 ? "nothing" : string.Join(", ", diagnostics.Select(d => d.Id + ": " + d.GetMessage())))}");
        }

        private static void AssertClean(string body)
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(body);

            Assert.True(
                diagnostics.Count == 0,
                "expected no diagnostics, got: "
                    + string.Join(", ", diagnostics.Select(d => d.Id + ": " + d.GetMessage())));
        }

        [Fact]
        public void APlainTypeReportsNothing()
        {
            AssertClean(@"
public class Plain { public int Id { get; set; } public string Name { get; set; } }

[CborSerializable(typeof(Plain))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The four types classified as primitives that have no case in PrimitiveConverterProvider.
        /// Each would fall through to ObjectConverterProvider and reach MakeGenericType at run time —
        /// the exact failure a generated context exists to prevent, and invisible to the AOT analyzer.
        /// </summary>
        [Theory]
        [InlineData("object")]
        [InlineData("System.Guid")]
        [InlineData("System.DateTimeOffset")]
        [InlineData("System.Half")]
        public void TypesWithNoConcreteConverterAreReported(string memberType)
        {
            AssertReports("CBOR1002", $@"
public class Holder {{ public {memberType} Value {{ get; set; }} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        [Theory]
        [InlineData("[CborConverter(typeof(Custom))]", "type")]
        [InlineData("[CborNamingConvention(typeof(object))]", "type")]
        [InlineData("[CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]", "type")]
        public void UnsupportedTypeLevelFeaturesAreReported(string attribute, string _)
        {
            AssertReports("CBOR1007", $@"
public class Custom {{ }}

{attribute}
public class Holder {{ public int Value {{ get; set; }} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        [Theory]
        [InlineData("[CborRequired]")]
        [InlineData("[CborIgnoreIfDefault]")]
        [InlineData("[DefaultValue(3)]")]
        [InlineData("[CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]")]
        public void UnsupportedMemberLevelFeaturesAreReported(string attribute)
        {
            AssertReports("CBOR1007", $@"
public class Holder {{ {attribute} public int Value {{ get; set; }} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        /// <summary>
        /// A user's own converter being dropped without a word is the worst of the ten, because the
        /// caller has explicitly said how the type must be encoded.
        /// </summary>
        [Fact]
        public void ACustomConverterOnAMemberIsReported()
        {
            AssertReports("CBOR1007", @"
public class Custom { }

public class Holder { [CborConverter(typeof(Custom))] public int Value { get; set; } }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void DeserializationCallbacksAreReported()
        {
            AssertReports("CBOR1007", @"
public class Holder
{
    public int Value { get; set; }

    [OnDeserialized]
    public void AfterRead() { }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void AShouldSerializeMethodIsReported()
        {
            AssertReports("CBOR1007", @"
public class Holder
{
    public int Value { get; set; }

    public bool ShouldSerializeValue() => true;
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The reflection path serializes internal properties, internal and protected fields, and any
        /// non-public member carrying [CborProperty]. Dropping one changes the wire format.
        /// </summary>
        [Theory]
        [InlineData("internal int Value { get; set; }")]
        [InlineData("internal int Value;")]
        [InlineData("protected int Value;")]
        [InlineData("[CborProperty(\"v\")] private int Value;")]
        public void NonPublicMembersTheReflectionPathSerializesAreReported(string member)
        {
            AssertReports("CBOR1008", $@"
public class Holder {{ public int Kept {{ get; set; }} {member} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        /// <summary>
        /// A private field with no attribute is not serialized by the reflection path either, so
        /// dropping it is not a divergence and must not be reported.
        /// </summary>
        [Fact]
        public void APrivateFieldWithoutAnAttributeIsNotReported()
        {
            AssertClean(@"
public class Holder { public int Kept { get; set; } private int _scratch; }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// Base to derived is not a member edge, so a subtype is never reached by walking outwards
        /// from a root and would silently fail to resolve on a polymorphic read.
        /// </summary>
        [Fact]
        public void ADiscriminatedSubtypeThatIsNotDeclaredIsReported()
        {
            AssertReports("CBOR1009", @"
[CborDiscriminator(""base"")]
public class Animal { public int Id { get; set; } }

[CborDiscriminator(""dog"")]
public class Dog : Animal { public string Breed { get; set; } }

[CborSerializable(typeof(Animal))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void ADeclaredSubtypeIsNotReported()
        {
            AssertClean(@"
[CborDiscriminator(""base"")]
public class Animal { public int Id { get; set; } }

[CborDiscriminator(""dog"")]
public class Dog : Animal { public string Breed { get; set; } }

[CborSerializable(typeof(Animal))]
[CborSerializable(typeof(Dog))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The reflection path can reach a non-public constructor or a creator mapping; a generated
        /// factory is a plain new T(), so this fails at run time with nothing said at build time.
        /// </summary>
        [Fact]
        public void ATypeWithNoAccessibleParameterlessConstructorIsReported()
        {
            AssertReports("CBOR1010", @"
public class Holder
{
    public Holder(int value) { Value = value; }

    public int Value { get; set; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// Two members under one CBOR name. #186 made the reflection path refuse the mapping when it is
        /// built, so such a type cannot serialize at all; the generator is where it can be said first,
        /// against the second declaration rather than at the first construction of the context.
        /// </summary>
        [Fact]
        public void TwoMembersUnderOneCborNameAreReported()
        {
            AssertReports("CBOR1013", @"
public class Aliased
{
    [CborProperty(""X"")] public int First { get; set; }
    [CborProperty(""X"")] public int Second { get; set; }
}

[CborSerializable(typeof(Aliased))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The other route the generator can see, and the one worth catching early: a naming policy
        /// folding two member names into one, where nothing is a duplicate as declared.
        /// </summary>
        /// <remarks>
        /// The policy has to be the context-level one. A <c>[CborNamingConvention]</c> on the type is
        /// already CBOR1007 — the generator does not reproduce it at all — so that route cannot reach
        /// this check, and the run-time refusal remains the only thing covering it.
        /// </remarks>
        [Fact]
        public void ANamingPolicyFoldingTwoNamesIntoOneIsReported()
        {
            AssertReports("CBOR1013", @"
public class Folded
{
    public int Value { get; set; }
    public int value { get; set; }
}

[CborSerializable(typeof(Folded))]
[CborSourceGenerationOptions(
    NamingConvention = typeof(Dahomey.Cbor.Serialization.Conventions.CamelCaseNamingConvention))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The same failure where the wire key is the index rather than the name, which is what
        /// <c>IntKeyMap</c> and <c>Array</c> are.
        /// </summary>
        [Fact]
        public void TwoMembersUnderOneCborIndexAreReported()
        {
            AssertReports("CBOR1014", @"
[CborObjectFormat(CborObjectFormat.IntKeyMap)]
public class Indexed
{
    [CborProperty(1)] public int First { get; set; }
    [CborProperty(1)] public int Second { get; set; }
}

[CborSerializable(typeof(Indexed))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// An overridden property is one member, not two. It is declared on both the base and the
        /// derived type, so the member walk sees it twice, and reporting that would turn every type
        /// with a virtual property into a build error — where the reflection path sees one property,
        /// since <c>Type.GetProperties</c> collapses an override onto its base.
        /// </summary>
        [Fact]
        public void AnOverriddenPropertyIsNotReported()
        {
            AssertClean(@"
public class Shape { public virtual int Id { get; set; } }

public class Circle : Shape { public override int Id { get; set; } }

[CborSerializable(typeof(Circle))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// An abstract base is never instantiated — its subtypes are — so it must not be reported.
        /// </summary>
        [Fact]
        public void AnAbstractBaseIsNotReported()
        {
            AssertClean(@"
public abstract class Shape { public int Id { get; set; } }

public class Circle : Shape { public double Radius { get; set; } }

[CborSerializable(typeof(Shape))]
[CborSerializable(typeof(Circle))]
public partial class Context : CborSerializerContext { }
");
        }
    }
}
