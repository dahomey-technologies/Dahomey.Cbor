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
        /// The types with no case in PrimitiveConverterProvider. Each would fall through to
        /// ObjectConverterProvider and reach MakeGenericType at run time — the exact failure a generated
        /// context exists to prevent, and invisible to the AOT analyzer.
        /// </summary>
        [Theory]
        [InlineData("object")]
        [InlineData("System.Guid")]
        [InlineData("System.DateTimeOffset")]
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
            AssertReports("CBOR1007", @"
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
                GeneratorHarness.RunAgainstLibrary(
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
            string Message(string accessibility) => GeneratorHarness.RunAgainstLibrary(
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
            AssertReports("CBOR1007", Listed(format, attribute, "int a, int b"));
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
            AssertReports("CBOR1007", @"
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
