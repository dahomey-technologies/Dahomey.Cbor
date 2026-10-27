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

        private const string InheritedFormatPreamble = @"
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
";

        /// <summary>
        /// A base that lives in another assembly reaches the generator as a metadata symbol, whose
        /// attributes are read from the compiled image rather than from source. The inherited format
        /// has to be found there too.
        /// </summary>
        [Fact]
        public void AFormatInheritedFromAnotherAssemblyIsStated()
        {
            (IReadOnlyList<Diagnostic> diagnostics, string generated) = GeneratorHarness.RunAgainstLibrary(
                InheritedFormatPreamble + @"
namespace Lib { [CborObjectFormat(CborObjectFormat.Array)] public class ArrBase { [CborProperty(0)] public int Id { get; set; } } }",
                InheritedFormatPreamble + @"
public class ArrDerived : Lib.ArrBase { [CborProperty(1)] public string Name { get; set; } }

[CborSerializable(typeof(ArrDerived))]
public partial class Context : CborSerializerContext { }
");

            Assert.Empty(diagnostics);
            Assert.Contains("SetObjectFormat(CborObjectFormat.Array)", generated);
        }

        [Fact]
        public void ABaseFromAnotherAssemblyWithNoFormatLeavesTheSubtypeUnstated()
        {
            (IReadOnlyList<Diagnostic> diagnostics, string generated) = GeneratorHarness.RunAgainstLibrary(
                InheritedFormatPreamble + @"
namespace Lib { public class Base { public int Id { get; set; } } }",
                InheritedFormatPreamble + @"
public class Derived : Lib.Base { public string Name { get; set; } }

[CborSerializable(typeof(Derived))]
public partial class Context : CborSerializerContext { }
");

            Assert.Empty(diagnostics);
            Assert.DoesNotContain("SetObjectFormat", generated);
        }

        /// <summary>
        /// The mutation target for a walk that stops at the immediate base: only the grandparent
        /// declares the format.
        /// </summary>
        [Fact]
        public void AFormatDeclaredByAGrandparentIsStated()
        {
            string generated = GeneratorHarness.GeneratedSource(Preamble + @"
[CborObjectFormat(CborObjectFormat.Array)]
public class Grand { [CborProperty(0)] public int Id { get; set; } }
public class Parent : Grand { [CborProperty(1)] public string Name { get; set; } }
public class Leaf : Parent { [CborProperty(2)] public int Extra { get; set; } }

[CborSerializable(typeof(Leaf))]
public partial class Context : CborSerializerContext { }
");

            Assert.Contains("SetObjectFormat(CborObjectFormat.Array)", generated);
        }

        [Fact]
        public void TheIndexDiagnosticSaysWhereAnInheritedFormatCameFrom()
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(@"
[CborObjectFormat(CborObjectFormat.Array)]
public class ArrBase { [CborProperty(0)] public int Id { get; set; } }
public class ArrDerived : ArrBase { public string Name { get; set; } }

[CborSerializable(typeof(ArrDerived))]
public partial class Context : CborSerializerContext { }
");

            Diagnostic missing = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "CBOR1005");
            Assert.Contains("inherited from 'ArrBase'", missing.GetMessage());
        }

        /// <summary>
        /// Reflection honours a base's [CborNamingConvention] and [CborLengthMode] on its subtypes
        /// (see <c>InheritedTypeAttributeReflectionTests</c>), so a subtype of one is as unsupported as
        /// the base is -- and was reported for the base only.
        /// </summary>
        [Theory]
        [InlineData("[CborNamingConvention(typeof(Dahomey.Cbor.Serialization.Conventions.CamelCaseNamingConvention))]", "[CborNamingConvention] (inherited from Base)")]
        [InlineData("[CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]", "[CborLengthMode] (inherited from Base)")]
        public void AnAttributeInheritedFromABaseIsReportedAsUnsupported(string attribute, string expected)
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(@"
" + attribute + @"
public class Base { public int Id { get; set; } }
public class Derived : Base { public string Name { get; set; } }

[CborSerializable(typeof(Derived))]
public partial class Context : CborSerializerContext { }
");

            Assert.Contains(
                diagnostics,
                diagnostic => diagnostic.Id == "CBOR1007" && diagnostic.GetMessage().Contains(expected));
        }

        /// <summary>
        /// A base with one discriminator kind and a subtype with the other is the both-attributes
        /// conflict reflection throws for, because it reads the base's by inheritance.
        /// </summary>
        [Fact]
        public void ASubtypeWithTheOtherDiscriminatorKindThanItsBaseConflicts()
        {
            AssertReports("CBOR1004", @"
[CborDiscriminator(""a"")]
public class Base { public int Id { get; set; } }
[CborIntDiscriminator(1)]
public class Derived : Base { public string Name { get; set; } }

[CborSerializable(typeof(Base))]
[CborSerializable(typeof(Derived))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// [CborObjectFormat] is inherited on the reflection path, so a subtype of an Array base is an
        /// Array type and its members need indexes there too. CBOR1005 is what the reflection path's
        /// "expecting all fields/properties to get a member index" becomes at build time.
        /// </summary>
        [Fact]
        public void ASubtypeOfAnArrayBaseNeedsMemberIndexes()
        {
            AssertReports("CBOR1005", @"
[CborObjectFormat(CborObjectFormat.Array)]
public class ArrBase { [CborProperty(0)] public int Id { get; set; } }
public class ArrDerived : ArrBase { public string Name { get; set; } }

[CborSerializable(typeof(ArrDerived))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void ASubtypeOfAnArrayBaseWithIndexesReportsNothing()
        {
            AssertClean(@"
[CborObjectFormat(CborObjectFormat.Array)]
public class ArrBase { [CborProperty(0)] public int Id { get; set; } }
public class ArrDerived : ArrBase { [CborProperty(1)] public string Name { get; set; } }

[CborSerializable(typeof(ArrDerived))]
public partial class Context : CborSerializerContext { }
");
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
        /// The types with no case in PrimitiveConverterProvider. Each would fall through to
        /// ObjectConverterProvider and reach MakeGenericType at run time — the exact failure a generated
        /// context exists to prevent, and invisible to the AOT analyzer.
        /// </summary>
        [Theory]
        [InlineData("object")]
        [InlineData("System.Guid")]
        public void TypesWithNoConcreteConverterAreReported(string memberType)
        {
            AssertReports("CBOR1002", $@"
public class Holder {{ public {memberType} Value {{ get; set; }} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        /// <summary>
        /// <c>HalfConverter</c> is a concrete converter, so a <c>Half</c> belongs on the other side of
        /// that line — scalar and as an RFC 8746 typed-array element, which is the tenth element type.
        /// </summary>
        /// <remarks>
        /// The absence of a diagnostic is only half of what this has to be right about: a type in
        /// neither <c>TypeCollector.IsPrimitive</c> nor <c>HasNoConcreteConverter</c> is also clean here,
        /// while being collected as an object and written as its internal members.
        /// <c>PrimitiveConverterProviderParityTests</c> in the library suite is what pins the other half.
        /// </remarks>
        [Theory]
        [InlineData("System.Half")]
        [InlineData("System.Half[]")]
        public void AHalfIsNotReported(string memberType)
        {
            AssertClean($@"
public class Holder {{ public {memberType} Value {{ get; set; }} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        [Theory]
        [InlineData("[CborConverter(typeof(Custom))]")]
        [InlineData("[CborNamingConvention(typeof(object))]")]
        [InlineData("[CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]")]
        public void UnsupportedTypeLevelFeaturesAreReported(string attribute)
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

        /// <summary>
        /// Both are expressible through the delegate mappings — <c>SetRequired</c> and
        /// <c>SetShouldSerializeMethod</c> have always been there — so refusing them was the generator
        /// not emitting a call it could make, rather than a shape it could not reproduce.
        /// </summary>
        [Fact]
        public void ARequiredMemberIsNotReported()
        {
            AssertClean(@"
public class Holder { [CborRequired] public int Value { get; set; } }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void AShouldSerializeMethodIsNotReported()
        {
            AssertClean(@"
public class Holder
{
    public int Value { get; set; }

    public bool ShouldSerializeValue() => true;
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        private static string ShouldSerializeHolder(string declarations) => $@"
public class Holder
{{
    public int Value {{ get; set; }}

    {declarations}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
";

        /// <summary>
        /// The shapes where the reflection path throws while building the mapping, each confirmed
        /// against it by <c>ShouldSerializeReflectionRefusalTests</c> in the library tests: a
        /// <c>public bool</c> predicate that is static or generic on the declaring type, and a
        /// generic and a non-generic version side by side.
        /// </summary>
        [Theory]
        [InlineData("public static bool ShouldSerializeValue() => false;")]
        [InlineData("public bool ShouldSerializeValue<T>() => false;")]
        [InlineData("public bool ShouldSerializeValue() => false; public bool ShouldSerializeValue<T>() => false;")]
        [InlineData("public int ShouldSerializeValue() => 0; public bool ShouldSerializeValue<T>() => false;")]
        public void AShouldSerializeMethodTheReflectionPathThrowsOnIsReported(string declarations)
        {
            AssertReports("CBOR1017", ShouldSerializeHolder(declarations));
        }

        [Fact]
        public void AGenericShouldSerializeMethodFoundOnABaseIsReported()
        {
            AssertReports("CBOR1017", @"
public class Base { public bool ShouldSerializeValue<T>() => false; }
public class Holder : Base { public int Value { get; set; } }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void AStaticShouldSerializeMethodOnAStructIsReported()
        {
            AssertReports("CBOR1017", @"
public struct Holder { public int Value { get; set; } public static bool ShouldSerializeValue() => false; }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        [Fact]
        public void AStaticShouldSerializeMethodOnTheBaseOfTheDeclaringTypeIsReportedAgainstAMemberItDeclares()
        {
            // Value is declared on Holder, whose own static is found first.
            AssertReports("CBOR1017", @"
public class Base { public bool ShouldSerializeValue() => false; }
public class Holder : Base { public int Value { get; set; } public new static bool ShouldSerializeValue() => true; }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The shapes the reflection path does not throw on, which must stay silent: a static
        /// inherited from a base is invisible to a lookup that is not FlattenHierarchy, a method that
        /// does not return bool is ignored whatever else it is, and a non-public one is not seen.
        /// </summary>
        [Theory]
        [InlineData("public class Base { public static bool ShouldSerializeValue() => false; } public class Holder : Base { public int Value { get; set; } }")]
        [InlineData("public class Base { public bool ShouldSerializeValue() => false; } public class Mid : Base { public new static bool ShouldSerializeValue() => true; } public class Holder : Mid { public int Value { get; set; } }")]
        [InlineData("public class Holder { public int Value { get; set; } public static int ShouldSerializeValue() => 0; }")]
        [InlineData("public class Holder { public int Value { get; set; } public int ShouldSerializeValue<T>() => 0; }")]
        [InlineData("public class Holder { public int Value { get; set; } internal static bool ShouldSerializeValue() => false; }")]
        [InlineData("public class Holder { public int Value { get; set; } internal bool ShouldSerializeValue<T>() => false; public bool ShouldSerializeValue() => false; }")]
        [InlineData("public class Base { public bool ShouldSerializeValue<T>() => false; } public class Holder : Base { public int Value { get; set; } public bool ShouldSerializeValue() => false; }")]
        [InlineData("public struct Holder { public int Value { get; set; } public bool ShouldSerializeValue() => false; }")]
        public void AShouldSerializeShapeTheReflectionPathAcceptsIsNotReported(string types)
        {
            AssertClean(types + @"

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// A <c>ShouldSerialize</c> method the reflection path ignores — it takes a parameter, so it is
        /// not the convention — must not be picked up here either, or the two paths disagree about
        /// whether the member is written at all.
        /// </summary>
        [Fact]
        public void AShouldSerializeMethodTheReflectionPathIgnoresIsNotUsed()
        {
            AssertClean(@"
public class Holder
{
    public int Value { get; set; }

    public bool ShouldSerializeValue(int unused) => false;
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
            // Only a private constructor, so there is nothing to build the type with and nothing to
            // build it *through* either. A single accessible parameterised constructor is a different
            // case now -- it becomes the creator -- and has its own test.
            AssertReports("CBOR1010", @"
public class Holder
{
    private Holder() { }

    public int Value { get; set; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The case that changed: one accessible constructor, no parameterless one. It used to be
        /// CBOR1010 and is now the creator the type is read through.
        /// </summary>
        [Fact]
        public void ASingleParameterisedConstructorIsNotReported()
        {
            AssertClean(@"
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
        /// The same, under <c>Array</c>. The descriptor's summary claims both index-keyed formats and
        /// the branch is reached through <c>ObjectFormat != "StringKeyMap"</c>, so <c>IntKeyMap</c>
        /// alone leaves the other half of the claim resting on reading the condition.
        /// </summary>
        [Fact]
        public void TwoMembersUnderOneCborIndexAreReportedUnderArrayFormatToo()
        {
            AssertReports("CBOR1014", @"
[CborObjectFormat(CborObjectFormat.Array)]
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
        /// A member hiding a base member with <c>new</c> is two members, and stays reported. That is why
        /// the collapse filters on <c>IsOverride</c> rather than on the name: <c>Type.GetProperties</c>
        /// returns both declarations, so the reflection path sees two members under one name and refuses
        /// the mapping, and the generator gives the same answer earlier. Widening the filter to names
        /// would turn this into a silent drop of whichever declaration came second.
        /// </summary>
        [Fact]
        public void AMemberHidingABaseMemberWithNewIsReported()
        {
            AssertReports("CBOR1013", @"
public class Shape { public int Id { get; set; } }

public class Circle : Shape { public new int Id { get; set; } }

[CborSerializable(typeof(Circle))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The collapse through a <em>constructed</em> generic base, which is the fragile half of the
        /// mechanism: it leans on <c>OverriddenProperty</c> yielding the member of <c>Holder&lt;int&gt;</c>
        /// and on <c>SymbolEqualityComparer.Default</c> matching that against what <c>GetMembers()</c>
        /// returns for the same constructed type. Without it this is CBOR1013, and every generated
        /// context over a type overriding a generic base member fails to build.
        /// </summary>
        [Fact]
        public void AnOverrideThroughAGenericBaseIsNotReported()
        {
            AssertClean(@"
public class Holder<T> { public virtual T Value { get; set; } }

public class IntHolder : Holder<int> { public override int Value { get; set; } }

[CborSerializable(typeof(IntHolder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// A positional record has no parameterless constructor and `init`-only properties, so it failed
        /// twice over: nothing could build it, and nothing could set its members. Both stop mattering
        /// once the values arrive through the constructor.
        /// </summary>
        [Fact]
        public void APositionalRecordIsNotReported()
        {
            AssertClean(@"
public record R(int Id, string Name);

[CborSerializable(typeof(R))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// An `init`-only property the creator does not supply still has no way back, so it keeps its
        /// CBOR1006 — the suppression is per member, not per type.
        /// </summary>
        [Fact]
        public void AnInitOnlyMemberTheCreatorDoesNotSupplyIsStillReported()
        {
            AssertReports("CBOR1006", @"
public class Holder
{
    [CborConstructor]
    public Holder(int id) { Id = id; }

    public int Id { get; init; }
    public string Extra { get; init; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// Several constructors, none marked and none parameterless: the reflection path takes
        /// <c>constructorInfos[0]</c>, and nothing guarantees Roslyn orders them the same way. Picking
        /// one here could build the type through a different constructor than the reflection path uses,
        /// so this keeps CBOR1010 — whose remedy resolves the ambiguity for both paths at once.
        /// </summary>
        [Fact]
        public void SeveralUnmarkedConstructorsAreStillReported()
        {
            AssertReports("CBOR1010", @"
public class Holder
{
    public Holder(int id) { Id = id; }
    public Holder(int id, string name) { Id = id; Name = name; }

    public int Id { get; set; }
    public string Name { get; set; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// A parameter naming no serialized member cannot be filled from a document. The reflection path
        /// leaves it at its default; emitting a creator that silently drops it would make the two paths
        /// agree on the bytes and disagree on what the object comes back holding, which is worse.
        /// </summary>
        [Fact]
        public void ACreatorParameterMatchingNoMemberIsReported()
        {
            AssertReports("CBOR1016", @"
public class Holder
{
    [CborConstructor]
    public Holder(int id, string absent) { Id = id; }

    public int Id { get; set; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        private static void AssertReportsAndEmitsNothingUncompilable(string id, string body)
        {
            AssertReports(id, body);

            // The diagnostic is an error, so the build stops there; this is the other half of the
            // contract: nothing is emitted that would also fail with CS0122.
            Assert.DoesNotContain(
                GeneratorHarness.CompileGeneratedOutput(Preamble + body),
                d => d.Id == "CS0122");
        }

        /// <summary>
        /// The generated context calls the constructor from its own class, so <c>protected</c> and
        /// <c>private</c> ones are out of reach -- CS0122 in the emitted file if nothing says so.
        /// </summary>
        [Theory]
        [InlineData("protected")]
        [InlineData("private")]
        [InlineData("private protected")]
        public void AMarkedCreatorTheContextCannotCallIsReported(string accessibility)
        {
            AssertReportsAndEmitsNothingUncompilable("CBOR1015", $@"
public class Holder
{{
    [CborConstructor]
    {accessibility} Holder(int id) {{ Id = id; }}

    public Holder() {{ }}

    public int Id {{ get; set; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        [Theory]
        [InlineData("protected")]
        [InlineData("private")]
        [InlineData("private protected")]
        public void ASoleCreatorTheContextCannotCallIsReported(string accessibility)
        {
            AssertReportsAndEmitsNothingUncompilable("CBOR1015", $@"
public class Holder
{{
    {accessibility} Holder(int id) {{ Id = id; }}

    public int Id {{ get; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        /// <summary>
        /// The same hole existed for <c>new T()</c>: a protected parameterless constructor counted as
        /// accessible because only <c>private</c> was excluded.
        /// </summary>
        [Theory]
        [InlineData("protected")]
        [InlineData("private protected")]
        public void AProtectedParameterlessConstructorIsReported(string accessibility)
        {
            AssertReportsAndEmitsNothingUncompilable("CBOR1010", $@"
public class Holder
{{
    {accessibility} Holder() {{ }}

    public int Id {{ get; set; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        /// <summary>The accessible routes still work, and the output compiles.</summary>
        [Theory]
        [InlineData("public")]
        [InlineData("internal")]
        [InlineData("protected internal")]
        public void ACreatorTheContextCanCallIsNotReported(string accessibility)
        {
            string body = $@"
public class Holder
{{
    {accessibility} Holder(int id) {{ Id = id; }}

    public int Id {{ get; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
";
            AssertClean(body);
            Assert.Empty(GeneratorHarness.CompileGeneratedOutput(Preamble + body));
        }

        /// <summary>
        /// <c>internal</c> means "this assembly", so the same declaration is reachable from a context
        /// in the declaring assembly and not from one in another. Only the compilation can tell.
        /// </summary>
        [Theory]
        [InlineData("internal", true)]
        [InlineData("protected internal", false)]
        [InlineData("public", false)]
        public void AnInternalCreatorInAnotherAssemblyIsReported(string accessibility, bool expectReport)
        {
            (IReadOnlyList<Diagnostic> reported, IReadOnlyList<Diagnostic> errors) =
                GeneratorHarness.RunAgainstLibraryAndCompile(
                    Preamble + $@"
public class Holder
{{
    {accessibility} Holder(int id) {{ Id = id; }}

    public int Id {{ get; }}
}}
",
                    Preamble + @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");

            // protected internal is protected OR internal, so from another assembly it is protected.
            bool inaccessible = expectReport || accessibility == "protected internal";

            Assert.Equal(inaccessible, reported.Any(d => d.Id == "CBOR1015"));
            Assert.DoesNotContain(errors, d => d.Id == "CS0122");
        }

        /// <summary>
        /// <c>IsSymbolAccessibleWithin</c> alone says a protected constructor is reachable from a type
        /// nested in a <em>derived</em> class, which it is only through a <c>base(...)</c> call --
        /// <c>new Holder(...)</c> there is still CS0122. The check has to be made through the type.
        /// </summary>
        [Theory]
        [InlineData("[CborConstructor] protected Holder(int id) { Id = id; }", "CBOR1015")]
        [InlineData("protected Holder(int id) { Id = id; }", "CBOR1015")]
        [InlineData("[CborConstructor] private protected Holder(int id) { Id = id; }", "CBOR1015")]
        [InlineData("protected Holder() { }", "CBOR1010")]
        [InlineData("private protected Holder() { }", "CBOR1010")]
        public void AProtectedConstructorIsNotReachableFromAContextNestedInADerivedType(string constructor, string id)
        {
            AssertReportsAndEmitsNothingUncompilable(id, $@"
public class Holder
{{
    {constructor}

    public int Id {{ get; set; }}
}}

public partial class Derived : Holder
{{
    public Derived() : base(0) {{ }}

    [CborSerializable(typeof(Holder))]
    public partial class Context : CborSerializerContext {{ }}
}}
");
        }

        /// <summary>The other side of the same check: a context nested in the type itself may call it.</summary>
        [Theory]
        [InlineData("[CborConstructor] protected Holder(int id) { Id = id; }")]
        [InlineData("[CborConstructor] private Holder(int id) { Id = id; }")]
        [InlineData("protected Holder() { }")]
        public void AContextNestedInTheTypeItselfMayCallItsConstructor(string constructor)
        {
            string body = $@"
public partial class Holder
{{
    {constructor}

    public int Id {{ get; set; }}

    [CborSerializable(typeof(Holder))]
    public partial class Context : CborSerializerContext {{ }}
}}
";
            AssertClean(body);
            Assert.Empty(GeneratorHarness.CompileGeneratedOutput(Preamble + body));
        }

        /// <summary>
        /// The remedy for an unreachable creator is the constructor, not a setter, so the warning that
        /// says otherwise on the members it would have supplied is suppressed.
        /// </summary>
        [Fact]
        public void AnUnreachableCreatorDoesNotAlsoAskForSetters()
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(@"
public class Holder
{
    [CborConstructor]
    protected Holder(int id) { Id = id; }

    public int Id { get; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");

            Assert.Contains(diagnostics, d => d.Id == "CBOR1015");
            Assert.DoesNotContain(diagnostics, d => d.Id == "CBOR1006");
        }

        [Theory]
        [InlineData("private", "a private constructor")]
        [InlineData("protected", "a protected constructor")]
        [InlineData("private protected", "a private protected constructor")]
        public void TheAccessibilityIsNamedInTheMessage(string accessibility, string expected)
        {
            IReadOnlyList<Diagnostic> diagnostics = Run($@"
public class Holder
{{
    [CborConstructor]
    {accessibility} Holder(int id) {{ Id = id; }}

    public int Id {{ get; set; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");

            Assert.Contains(expected, diagnostics.Single(d => d.Id == "CBOR1015").GetMessage());
        }

        [Fact]
        public void TheMessageNamesAnInternalAndAProtectedInternalCreatorOfAnotherAssembly()
        {
            string Message(string accessibility) => GeneratorHarness.RunAgainstLibraryAndCompile(
                Preamble + $@"
public class Holder
{{
    [CborConstructor]
    {accessibility} Holder(int id) {{ Id = id; }}

    public int Id {{ get; set; }}
}}
",
                Preamble + @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
").Reported.Single(d => d.Id == "CBOR1015").GetMessage();

            Assert.Contains("an internal constructor", Message("internal"));
            Assert.Contains("a protected internal constructor", Message("protected internal"));
        }

        /// <summary>
        /// The reflection path calls a private parameterless constructor, which a generated context
        /// cannot -- the same type used to work by quietly reading through the other constructor,
        /// which is not the one reflection runs. Reported, with the remedy that makes both agree.
        /// </summary>
        [Fact]
        public void APrivateParameterlessConstructorBesideAPublicOneIsStillReported()
        {
            AssertReports("CBOR1010", @"
public class Holder
{
    private Holder() { }
    public Holder(int id) { Id = id; }

    public int Id { get; set; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// A record's copy constructor is never the creator, whether the compiler wrote it or the
        /// author did: the reflection path takes the primary one (declared first), and a sealed
        /// record's explicit private copy constructor must not turn the record into "several".
        /// </summary>
        [Fact]
        public void ASealedRecordWithAnExplicitPrivateCopyConstructorIsBuiltThroughItsPrimaryOne()
        {
            string body = @"
public sealed record R(int Id)
{
    private R(R original) { Id = original.Id; }
}

[CborSerializable(typeof(R))]
public partial class Context : CborSerializerContext { }
";
            AssertClean(body);
            Assert.Empty(GeneratorHarness.CompileGeneratedOutput(Preamble + body));
        }

        /// <summary>
        /// In a class the same shape is two ordinary constructors and which one the reflection path
        /// takes is not something to guess at.
        /// </summary>
        [Fact]
        public void AClassWithACopyShapedConstructorBesideAnotherIsStillAmbiguous()
        {
            AssertReports("CBOR1010", @"
public class Holder
{
    public Holder(Holder other) { Id = other.Id; }
    public Holder(int id) { Id = id; }

    public int Id { get; set; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        private static string Listed(string format, string attribute, string parameters) => $@"
{(format == "StringKeyMap" ? "" : $"[CborObjectFormat(CborObjectFormat.{format})]")}
public class Holder
{{
    {attribute}
    public Holder({parameters}) {{ A = a; B = b; }}

    {(format == "StringKeyMap" ? "" : "[CborProperty(0)]")} public int A {{ get; }}
    {(format == "StringKeyMap" ? "" : "[CborProperty(1)]")} public int B {{ get; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
";

        /// <summary>
        /// <c>[CborConstructor(...)]</c> arguments are positional and the reflection path honours them.
        /// Matching by parameter name instead reads the two crossed-over members the other way round,
        /// silently -- so they are carried into the emitted creator verbatim.
        /// </summary>
        [Theory]
        [InlineData("StringKeyMap", "[CborConstructor(\"B\", \"A\")]", ".SetMemberNames(\"B\", \"A\")")]
        [InlineData("IntKeyMap", "[CborConstructor(1, 0)]", ".SetMemberIndexes(1, 0)")]
        [InlineData("Array", "[CborConstructor(1, 0)]", ".SetMemberIndexes(1, 0)")]
        public void ListedConstructorArgumentsReachTheEmittedCreator(string format, string attribute, string expected)
        {
            string source = Preamble + Listed(format, attribute, "int a, int b");

            Assert.Empty(Run(Listed(format, attribute, "int a, int b")));
            Assert.Contains(expected, GeneratorHarness.GeneratedSource(source));
        }

        [Theory]
        [InlineData("StringKeyMap", "[CborConstructor(\"A\")]")]
        [InlineData("StringKeyMap", "[CborConstructor(\"A\", \"Missing\")]")]
        [InlineData("IntKeyMap", "[CborConstructor(0)]")]
        [InlineData("IntKeyMap", "[CborConstructor(0, 7)]")]
        [InlineData("Array", "[CborConstructor(0, 7)]")]
        public void ListedConstructorArgumentsTheReflectionPathRefusesAreReported(string format, string attribute)
        {
            AssertReports("CBOR1016", Listed(format, attribute, "int a, int b"));
        }

        /// <summary>
        /// The other kind of argument is ignored by the reflection path, which falls back to matching
        /// by parameter name -- so the generator must too, not read them as a refusal or as an order.
        /// </summary>
        [Theory]
        [InlineData("StringKeyMap", "[CborConstructor(1, 0)]")]
        [InlineData("IntKeyMap", "[CborConstructor(\"B\", \"A\")]")]
        public void ArgumentsOfTheOtherKindFallBackToMatchingByName(string format, string attribute)
        {
            string source = Preamble + Listed(format, attribute, "int a, int b");

            Assert.Empty(Run(Listed(format, attribute, "int a, int b")));

            string generated = GeneratorHarness.GeneratedSource(source);
            Assert.Contains(format == "StringKeyMap" ? ".SetMemberNames(\"A\", \"B\")" : ".SetMemberIndexes(0, 1)", generated);
        }

        /// <summary>CreatorMapping compares the two types exactly and throws when they differ.</summary>
        [Fact]
        public void AParameterOfAnotherTypeThanItsMemberIsReported()
        {
            AssertReports("CBOR1016", @"
public class Holder
{
    [CborConstructor]
    public Holder(long id) { Id = (int)id; }

    public int Id { get; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The collection interfaces the reflection path resolves through
        /// <c>InterfaceCollectionConverter</c>, and <c>IDictionary&lt;K,V&gt;</c> through
        /// <c>InterfaceDictionaryConverter</c>. Declaring one is ordinary — a model that says
        /// <c>IList&lt;T&gt;</c> rather than <c>List&lt;T&gt;</c> is expressing a contract, not being
        /// awkward — so refusing it made the generated path unusable for that model.
        /// </summary>
        [Theory]
        [InlineData("System.Collections.Generic.IList<int>")]
        [InlineData("System.Collections.Generic.ICollection<int>")]
        [InlineData("System.Collections.Generic.IEnumerable<int>")]
        [InlineData("System.Collections.Generic.IReadOnlyList<int>")]
        [InlineData("System.Collections.Generic.IReadOnlyCollection<int>")]
        [InlineData("System.Collections.Generic.ISet<int>")]
        [InlineData("System.Collections.Generic.IDictionary<string, int>")]
        public void ACollectionInterfaceMemberIsNotReported(string memberType)
        {
            AssertClean($@"
public class Holder {{ public {memberType} Value {{ get; set; }} }}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        private static string MessageOf(string id, string body)
        {
            return Run(body).First(d => d.Id == id).GetMessage();
        }

        private static IReadOnlyList<Diagnostic> CompileErrorsOf(string body)
        {
            return GeneratorHarness.CompileGeneratedOutput(Preamble + body);
        }

        private const string SnakeContext = @"
[CborSerializable(typeof(Holder))]
[CborSourceGenerationOptions(NamingConvention = typeof(Dahomey.Cbor.Serialization.Conventions.SnakeCaseNamingConvention))]
public partial class Context : CborSerializerContext { }
";

        /// <summary>
        /// A naming convention (or <c>[CborProperty("x")]</c>) renames the member, so the unlisted
        /// parameter no longer matches its CBOR name. The reflection path leaves such a parameter at
        /// its default and lets the member's setter supply the value, and that is not an error.
        /// </summary>
        [Theory]
        [InlineData("public record Holder(int FirstName, int Age);", SnakeContext)]
        [InlineData("public record Holder([property: CborProperty(\"fn\")] int FirstName, int Age);", "[CborSerializable(typeof(Holder))] public partial class Context : CborSerializerContext { }")]
        [InlineData("public class Holder { public int FirstName { get; set; } public int Age { get; set; } public Holder(int firstName, int age) { } }", SnakeContext)]
        public void AParameterWhoseMemberWasRenamedIsNotReported(string declaration, string context)
        {
            string body = declaration + "\n" + context;

            Assert.Empty(Run(body));
            Assert.Empty(CompileErrorsOf(body));
        }

        /// <summary>
        /// The same rename on a member with no setter: reflection leaves the parameter at its default
        /// and nothing ever sets the member, so its value is lost -- which is reported.
        /// </summary>
        [Fact]
        public void AParameterWhoseRenamedMemberHasNoSetterIsReported()
        {
            AssertReports("CBOR1016", @"
public class Holder { public int FirstName { get; } public Holder(int firstName) { FirstName = firstName; } }
" + SnakeContext);
        }

        /// <summary>A refused creator also says why, so "add a setter" on what it fills is noise.</summary>
        [Fact]
        public void ARefusedCreatorDoesNotAlsoAskForSetters()
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(@"
public class Holder
{
    [CborConstructor]
    public Holder(string absent, int id) { Id = id; }

    public int Id { get; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");

            Assert.Contains(diagnostics, d => d.Id == "CBOR1016");
            // Id is what the refused creator would have filled.
            Assert.DoesNotContain(diagnostics, d => d.Id == "CBOR1006");
        }

        /// <summary>
        /// The runtime compares <c>System.Type</c>s, and tuple element names and <c>dynamic</c> are
        /// erased before it does -- symbol equality is not.
        /// </summary>
        [Theory]
        [InlineData("(int X, int Y)", "(int, int)")]
        [InlineData("(int, int)", "(int A, int B)")]
        [InlineData("string", "string?")]
        public void ParameterAndMemberTypesTheRuntimeSeesAsOneAreNotReported(string memberType, string parameterType)
        {
            string body = $@"
public class Holder
{{
    public Holder({parameterType} p) {{ P = p; }}

    public {memberType} P {{ get; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
";
            Assert.Empty(Run(body));
            Assert.Empty(CompileErrorsOf(body));
        }

        [Theory]
        [InlineData("int?", "int")]
        [InlineData("long", "int")]
        [InlineData("List<int>", "IList<int>")]
        public void ParameterAndMemberTypesTheRuntimeSeesApartAreReported(string memberType, string parameterType)
        {
            AssertReports("CBOR1016", $@"
public class Holder
{{
    public Holder({parameterType} p) {{ }}

    public {memberType} P {{ get; set; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
");
        }

        /// <summary>
        /// The message states what actually happens in each case. Most of these shapes make the
        /// reflection path throw when the mapping is built; only an unlisted, unmatched parameter is
        /// silently left at its default there.
        /// </summary>
        [Fact]
        public void TheMessageSaysWhatTheReflectionPathDoesInEachCase()
        {
            string refused = "the reflection path refuses this mapping with a CborException when it is built";

            Assert.Contains(refused, MessageOf("CBOR1016", Listed("IntKeyMap", "[CborConstructor(0)]", "int a, int b")));
            Assert.Contains(refused, MessageOf("CBOR1016", Listed("IntKeyMap", "[CborConstructor(0, 7)]", "int a, int b")));
            Assert.Contains(refused, MessageOf("CBOR1016", @"
public class Holder { public Holder(long p) { } public int P { get; set; } }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
"));

            string unmatched = MessageOf("CBOR1016", @"
public class Holder { [CborConstructor] public Holder(int id, string absent) { } public int Id { get; set; } }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");

            Assert.Contains("leave it at its default value", unmatched);
            Assert.DoesNotContain("refuses", unmatched);
            Assert.DoesNotContain("silently produce different bytes", unmatched);
        }

        /// <summary>
        /// Two explicit constructors, none marked, in a struct: the reflection path takes the first
        /// declared, and <c>new T()</c> plus setters reads nothing into a member that has no setter.
        /// </summary>
        [Fact]
        public void AStructWithSeveralConstructorsAndAMemberNoSetterReachesIsReported()
        {
            AssertReports("CBOR1010", @"
public struct Money
{
    public Money(string currency, decimal amount) { Currency = currency; Amount = amount; }
    public Money(decimal amount) { Currency = ""USD""; Amount = amount; }

    public string Currency { get; }
    public decimal Amount { get; }
}

[CborSerializable(typeof(Money))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>The same struct with every member settable is still read completely.</summary>
        [Fact]
        public void AStructWithSeveralConstructorsAndOnlySettableMembersIsNotReported()
        {
            AssertClean(@"
public struct Pair
{
    public Pair(int x) { X = x; Y = 0; }
    public Pair(int x, int y) { X = x; Y = y; }

    public int X { get; set; }
    public int Y { get; set; }
}

[CborSerializable(typeof(Pair))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// A struct whose only constructor leaves a member unsupplied was read as default-plus-setters
        /// before creators existed; building it through that constructor instead would hand the other
        /// member to a setter a struct cannot take.
        /// </summary>
        [Fact]
        public void AStructWhoseSoleConstructorLeavesAMemberUnsuppliedIsNotBuiltThroughIt()
        {
            string body = @"
public struct Pt
{
    public int X;
    public int Y;

    public Pt(int x) { X = x; Y = 0; }
}

[CborSerializable(typeof(Pt))]
public partial class Context : CborSerializerContext { }
";
            AssertClean(body);
            Assert.DoesNotContain("MapCreator", GeneratorHarness.GeneratedSource(Preamble + body));
        }

        [Fact]
        public void AStructWhoseConstructorSuppliesEveryMemberIsBuiltThroughIt()
        {
            string body = @"
public struct Pt
{
    public Pt(int x, int y) { X = x; Y = y; }

    public int X { get; }
    public int Y { get; }
}

[CborSerializable(typeof(Pt))]
public partial class Context : CborSerializerContext { }
";
            AssertClean(body);
            Assert.Contains("MapCreator", GeneratorHarness.GeneratedSource(Preamble + body));
        }

        private static string Wide(int count)
        {
            return string.Join(", ", Enumerable.Range(1, count).Select(i => $"int A{i:00}"));
        }

        /// <summary>
        /// <c>System.Func</c> stops at sixteen inputs; the emitted creator declares a delegate type of
        /// its own beyond that, as the reflection path's expression tree does.
        /// </summary>
        [Theory]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(20)]
        [InlineData(40)]
        public void AWideConstructorIsCleanAndTheOutputCompiles(int count)
        {
            string body = $@"
public record Wide({Wide(count)});

[CborSerializable(typeof(Wide))]
public partial class Context : CborSerializerContext {{ }}
";
            AssertClean(body);
            Assert.Empty(CompileErrorsOf(body));
        }

        /// <summary>
        /// The call would omit the modifier (CS1620), and the reflection path refuses a by-reference
        /// parameter when it builds the mapping. <c>in</c> compiles and works today on the generated
        /// path, but reflection throws for it too, so the two must not disagree.
        /// </summary>
        [Theory]
        [InlineData("ref int id", "")]
        [InlineData("out int id", "id = 0;")]
        [InlineData("in int id", "")]
        public void AByReferenceConstructorParameterIsReported(string parameter, string body)
        {
            string source = $@"
public class Holder
{{
    [CborConstructor]
    public Holder({parameter}) {{ {body} }}

    public int Id {{ get; set; }}
}}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext {{ }}
";
            AssertReports("CBOR1016", source);
            Assert.DoesNotContain(CompileErrorsOf(source), d => d.Id == "CS1620");
        }

        /// <summary>
        /// A required member must be set by an object initializer or by a constructor marked
        /// [SetsRequiredMembers]; a generated call is neither (CS9035).
        /// </summary>
        [Theory]
        [InlineData("public record Holder(int Id) { public required string Name { get; set; } }")]
        [InlineData("public class Holder { public Holder() { } public required string Name { get; set; } }")]
        [InlineData("public class Holder { [CborConstructor] public Holder(string name) { Name = name; } public required string Name { get; set; } }")]
        [InlineData("public class Holder { public Holder(string name) { Name = name; } public required string Name { get; set; } }")]
        [InlineData("public struct Holder { public required string Name { get; set; } }")]
        [InlineData("public class Base { public required string Name { get; set; } } public class Holder : Base { public Holder() { } }")]
        [InlineData("public class Holder { public Holder() { } public Holder(int id) { } public required string Name { get; set; } }")]
        public void ARequiredMemberTheConstructorCannotSatisfyIsReported(string declaration)
        {
            string source = declaration + @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
";
            IReadOnlyList<Diagnostic> reported = Run(source);

            Assert.Contains(reported, d => d.Id == "CBOR1016");
            Assert.DoesNotContain(reported, d => d.Id == "CBOR1010");
            Assert.DoesNotContain(CompileErrorsOf(source), d => d.Id == "CS9035");
        }

        [Theory]
        [InlineData("public class Holder { [System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public Holder() { } public required string Name { get; set; } }")]
        [InlineData("public class Holder { [CborConstructor, System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public Holder(string name) { Name = name; } public required string Name { get; set; } }")]
        public void ARequiredMemberASetsRequiredMembersConstructorSatisfiesIsNotReported(string declaration)
        {
            string source = declaration + @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
";
            AssertClean(source);
            Assert.Empty(CompileErrorsOf(source));
        }

        /// <summary>
        /// The reflection path leaves the parameter at its default and runs the init accessor, so a
        /// constructor that transforms its argument sees the default there (reads 1001, not 2001).
        /// Passing the value through the constructor instead is only the same object when the
        /// parameter is assigned verbatim, which is true of a positional record's primary constructor
        /// and of nothing the generator can see in a hand-written one.
        /// </summary>
        [Fact]
        public void ARenamedInitOnlyMemberIsOnlyRoutedThroughARecordsPrimaryConstructor()
        {
            AssertReports("CBOR1016", @"
public class Holder
{
    [CborProperty(""first_name"")]
    public int FirstName { get; init; }

    public int Age { get; }

    public Holder(int firstName, int age) { FirstName = firstName + 1000; Age = age; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");

            AssertClean(@"
public record Holder([property: CborProperty(""first_name"")] int FirstName, int Age);

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>A record that declares its own constructor is not the positional one.</summary>
        [Fact]
        public void ARecordsExplicitConstructorIsNotARoutingSite()
        {
            AssertReports("CBOR1016", @"
public record Holder
{
    [CborProperty(""first_name"")]
    public int FirstName { get; init; }

    public Holder(int firstName) { FirstName = firstName + 1000; }
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// A read that has a creator never calls <c>new T()</c>, so the factory is not emitted for it --
        /// which is what lets a creator marked [SetsRequiredMembers] sit beside a plain parameterless
        /// constructor, and a struct (whose implicit constructor cannot carry the attribute) have a
        /// creator at all.
        /// </summary>
        [Theory]
        [InlineData("public class Holder { [CborConstructor, System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public Holder(string name) { Name = name; } public Holder() { } public required string Name { get; set; } }")]
        [InlineData("public struct Holder { [CborConstructor, System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public Holder(int id) { Id = id; } public required int Id { get; set; } }")]
        public void ARequiredMemberACreatorSatisfiesNeedsNoFactory(string declaration)
        {
            string source = declaration + @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
";
            AssertClean(source);
            Assert.Empty(CompileErrorsOf(source));
            Assert.DoesNotContain("() => new", GeneratorHarness.GeneratedSource(Preamble + source));
        }

        /// <summary>The same type without a creator still needs a factory, and still cannot have one.</summary>
        [Fact]
        public void ARequiredMemberStillRefusesTheFactoryWhenThereIsNoCreator()
        {
            AssertReports("CBOR1016", @"
public class Holder { [System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public Holder(string name) { Name = name; } public Holder() { } public required string Name { get; set; } }

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");
        }

        /// <summary>
        /// The compiler assigns a positional parameter to the property only when it synthesised the
        /// property itself. A property the record declares (initialised from the parameter however it
        /// likes), or one a derived record inherits from its base, is not assigned verbatim, so routing
        /// the value through the parameter would differ from the reflection path.
        /// </summary>
        [Theory]
        [InlineData("public record Holder(int FirstValue) { public int FirstValue { get; init; } = FirstValue * 2 + 1; }")]
        [InlineData("public record Base(int FirstValue) { public int FirstValue { get; init; } = FirstValue * 2 + 1; } public record Holder(int FirstValue, int Other) : Base(FirstValue);")]
        [InlineData("public record Base(int FirstValue) { public int FirstValue { get; init; } = FirstValue * 2 + 1; } public record Holder(int FirstValue, int Other) : Base(FirstValue) { }")]
        public void ARecordParameterTheCompilerDoesNotAssignVerbatimIsNotRouted(string declaration)
        {
            AssertReports("CBOR1016", declaration + "\n" + SnakeContext);
        }

        /// <summary>A synthesised property on the record itself, under a convention that renames it.</summary>
        [Fact]
        public void ASynthesisedPositionalPropertyIsStillRoutedUnderARenamingConvention()
        {
            string body = "public record Holder(int FirstValue, int Other);\n" + SnakeContext;

            AssertClean(body);
            Assert.Empty(CompileErrorsOf(body));
        }

        /// <summary>
        /// A creator and a refused ShouldSerialize method are separate problems on separate members:
        /// each is reported once, by its own diagnostic, and neither stands in for the other.
        /// </summary>
        [Fact]
        public void ACreatorAndARefusedShouldSerializeAreReportedSeparately()
        {
            IReadOnlyList<Diagnostic> diagnostics = Run(@"
public record Holder(int Id, string Name)
{
    public static bool ShouldSerializeName() => true;
}

[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
");

            Assert.Equal(new[] { "CBOR1017" }, diagnostics.Select(d => d.Id).Distinct());
        }

        /// <summary>
        /// The reflection path hands a renamed init-only parameter its default and sets the value
        /// through the init accessor afterwards. Passing it through the constructor is only the same
        /// object if nothing in the record reads the parameter: an initializer or a base-constructor
        /// argument that does (<c>_shadow = FirstValue * 2</c>) would see the value on the generated
        /// path and zero on the reflection one.
        /// </summary>
        [Theory]
        [InlineData("public record Holder(int FirstValue) { private readonly int _shadow = FirstValue * 2; public int Shadow() => _shadow; }")]
        [InlineData("public record Holder(int FirstValue) { public int Doubled { get; } = FirstValue * 2; }")]
        [InlineData("public record Base(int X) { public int X3 { get; } = X; } public record Holder(int FirstValue) : Base(FirstValue * 3);")]
        [InlineData("public record Holder(int FirstValue) { public int Twice { get; init; } = FirstValue + FirstValue; }")]
        public void ARecordParameterAnInitializerOrBaseArgumentReadsIsNotRouted(string declaration)
        {
            AssertReports("CBOR1016", declaration + "\n" + SnakeContext);
        }

        /// <summary>
        /// Inside a method body or an expression-bodied member a record's positional name is the
        /// property, not the parameter, so a "use" there does not read the parameter at all and the
        /// object is unaffected.
        /// </summary>
        [Theory]
        [InlineData("public record Holder(int FirstValue) { public int Twice() => FirstValue * 2; }")]
        [InlineData("public record Holder(int FirstValue) { public int Twice() { var v = FirstValue; return v * 2; } }")]
        [InlineData("public partial record Holder(int FirstValue); public partial record Holder { public int Twice() { return FirstValue + 1; } }")]
        public void ARecordParameterOnlyMethodBodiesNameIsStillRouted(string declaration)
        {
            string body = declaration + "\n" + SnakeContext;

            AssertClean(body);
            Assert.Empty(CompileErrorsOf(body));
        }

        /// <summary>A parameter read in another part of a partial record is still a read.</summary>
        [Fact]
        public void AParameterReadInAnotherPartOfAPartialRecordIsNotRouted()
        {
            AssertReports("CBOR1016", @"
public partial record Holder(int FirstValue);
public partial record Holder { private readonly int _shadow = FirstValue * 2; }
" + SnakeContext);
        }

        /// <summary>
        /// A diagnostic is an error, but the build still compiles what was generated and reports its
        /// errors too, in code the user never wrote. Whenever one of the creator diagnostics refuses
        /// a shape, nothing uncompilable may be emitted for it.
        /// </summary>
        [Theory]
        // CBOR1016: required members
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder() { Name = \"x\"; } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder() { Name = \"x\"; } public Holder(int id) { Name = \"y\"; } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { public Holder() { } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(string name) { Name = name; } public Holder() { } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public record Holder(int Id) { public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public struct Holder { public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public struct Holder { [CborConstructor] public Holder(int id) { Id = id; Name = null; } public int Id { get; set; } public required string Name { get; set; } }")]
        // CBOR1016: by-reference, wrong count, no matching member, type mismatch
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(ref int id) { } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(out int id) { id = 0; } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(in int id) { } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor(\"a\", \"b\")] public Holder(int a) { } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(int id, string absent) { Id = id; } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(long id) { } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborProperty(\"fn\")] public int FirstName { get; } public Holder(int firstName) { FirstName = firstName; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborProperty(\"fn\")] public int FirstName { get; init; } public Holder(int firstName) { FirstName = firstName + 1; } }")]
        // CBOR1015: unreachable creators
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] protected Holder(int id) { Id = id; } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] private Holder(int id) { Id = id; } public int Id { get; } }")]
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] private Holder() { Id = 1; } public Holder(int x) { } public int Id { get; set; } }")]
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] protected Holder() { Id = 1; } public int Id { get; set; } }")]
        // CBOR1010: nothing to build the type with
        [InlineData("CBOR1010", "public class Holder { private Holder() { } public Holder(int id) { Id = id; } public int Id { get; set; } }")]
        [InlineData("CBOR1010", "public class Holder { protected Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1010", "public class Holder { public Holder(Holder other) { } public Holder(int id) { Id = id; } public int Id { get; set; } }")]
        [InlineData("CBOR1010", "public struct Holder { public Holder(string c, decimal a) { C = c; A = a; } public Holder(decimal a) { C = \"x\"; A = a; } public string C { get; } public decimal A { get; } }")]
        public void EveryRefusalLeavesOutputThatCompiles(string id, string declaration)
        {
            string source = declaration + @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
";
            AssertReports(id, source);

            // The user's own declaration compiles, so any error here is in what was generated.
            Assert.Empty(CompileErrorsOf(source));
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
