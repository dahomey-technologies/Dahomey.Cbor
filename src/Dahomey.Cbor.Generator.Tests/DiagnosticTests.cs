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
