using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dahomey.Cbor.GeneratorTests
{
    /// <summary>
    /// A type is built through a constructor where <c>new T()</c> will not do. What the generator can
    /// reproduce is read through the constructor; what it cannot is a diagnostic, and in both cases the
    /// code it emits compiles -- the build still compiles what was generated beside an error.
    /// </summary>
    public class CreatorMappingTests
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
";

        private const string Context = @"
[CborSerializable(typeof(Holder))]
public partial class Context : CborSerializerContext { }
";

        private static IReadOnlyList<Diagnostic> Run(string declaration) =>
            GeneratorHarness.Run(Preamble + declaration + Context);

        private static string Generated(string declaration) =>
            GeneratorHarness.GeneratedSource(Preamble + declaration + Context);

        private static void AssertCompiles(string declaration) =>
            Assert.Empty(GeneratorHarness.CompileGeneratedOutput(Preamble + declaration + Context));

        [Theory]
        [InlineData("public record Holder(int Id, string Name);")]
        [InlineData("public sealed record Holder(int Id) { private Holder(Holder original) { Id = original.Id; } }")]
        [InlineData("public class Holder { internal Holder(int id) { Id = id; } public int Id { get; } }")]
        [InlineData("public class Holder { public Holder((int, int) p) { P = p; } public (int X, int Y) P { get; } }")]
        [InlineData("public class Holder { public Holder(string? p) { P = p; } public string P { get; } }")]
        [InlineData("public class Holder { [CborConstructor] public Holder(int id) { Id = id; } public Holder() { } public int Id { get; init; } }")]
        [InlineData("public class Holder { [CborConstructor, SetsRequiredMembers] public Holder(string name) { Name = name; } public Holder() { } public required string Name { get; set; } }")]
        [InlineData("public struct Holder { [CborConstructor, SetsRequiredMembers] public Holder(int id) { Id = id; } public required int Id { get; set; } }")]
        public void AnAcceptedShapeIsCleanAndCompiles(string declaration)
        {
            Assert.Empty(Run(declaration));
            AssertCompiles(declaration);
        }

        /// <summary>A context nested in the type itself may call even a protected constructor.</summary>
        [Fact]
        public void AContextNestedInTheTypeItselfMayCallItsConstructor()
        {
            string source = Preamble + @"
public partial class Holder
{
    [CborConstructor] protected Holder(int id) { Id = id; }

    public int Id { get; }

    [CborSerializable(typeof(Holder))]
    public partial class Context : CborSerializerContext { }
}";

            Assert.Empty(GeneratorHarness.Run(source));
            Assert.Empty(GeneratorHarness.CompileGeneratedOutput(source));
        }

        [Theory]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(40)]
        public void ConstructorsWiderThanFuncAreAccepted(int count)
        {
            string declaration = $"public record Holder({string.Join(", ", Enumerable.Range(1, count).Select(i => $"int A{i:00}"))});";

            Assert.Empty(Run(declaration));
            AssertCompiles(declaration);
        }

        /// <summary>A read that has a creator never calls <c>new T()</c>, and it would not compile beside a required member.</summary>
        [Fact]
        public void NoFactoryIsEmittedForATypeWithACreator()
        {
            Assert.DoesNotContain("() => new", Generated(
                "public class Holder { [CborConstructor] public Holder(int id) { Id = id; } public Holder() { } public int Id { get; } }"));
        }

        /// <summary>The reflection path refuses a struct its constructor does not fill; the generated path reads it as before.</summary>
        [Fact]
        public void AStructTheConstructorLeavesPartlyUnfilledIsNotBuiltThroughIt()
        {
            string declaration = "public struct Holder { public int X; public int Y; public Holder(int x) { X = x; Y = 0; } }";

            Assert.Empty(Run(declaration));
            Assert.DoesNotContain("MapCreator", Generated(declaration));
        }

        [Theory]
        // the context cannot call the constructor
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] protected Holder(int id) { Id = id; } public Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] private Holder(int id) { Id = id; } public int Id { get; } }")]
        [InlineData("CBOR1015", "public class Holder { private Holder(int id) { Id = id; } public int Id { get; } }")]
        [InlineData("CBOR1015", "public class Holder { [CborConstructor] private protected Holder() { } public Holder(int x) { } public int Id { get; set; } }")]
        // nothing to build the type with, or several constructors and no way to choose
        [InlineData("CBOR1010", "public class Holder { protected Holder() { } public int Id { get; set; } }")]
        [InlineData("CBOR1010", "public class Holder { private Holder() { } public Holder(int id) { Id = id; } public int Id { get; set; } }")]
        [InlineData("CBOR1010", "public class Holder { public Holder(int id) { Id = id; } public Holder(int id, int o) { Id = id; } public int Id { get; set; } }")]
        // the reflection path refuses the mapping, or would leave the parameter at its default
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(ref int id) { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(out int id) { id = 0; } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(in int id) { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public struct Holder { [CborConstructor] public Holder(in int id) { Id = id; } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public struct Holder { public int X; public int Y; [CborConstructor] public Holder(int x) { X = x; Y = 0; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor(\"a\", \"b\")] public Holder(int a) { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor(\"Id\", \"Missing\")] public Holder(int a, int b) { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { public Holder(int id, string absent) { Id = id; } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { public Holder(long id) { } public int Id { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { public Holder(int? id) { } public int Id { get; set; } }")]
        // a required member only an object initializer, or a [SetsRequiredMembers] constructor, can set
        [InlineData("CBOR1016", "public record Holder(int Id) { public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { public Holder() { } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder() { Name = \"x\"; } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Holder { [CborConstructor] public Holder(string name) { Name = name; } public Holder() { } public required string Name { get; set; } }")]
        [InlineData("CBOR1016", "public class Base { public required string Name { get; set; } } public class Holder : Base { public Holder() { } }")]
        [InlineData("CBOR1016", "public struct Holder { public required string Name { get; set; } }")]
        public void AShapeItCannotReproduceIsReportedAndLeavesOutputThatCompiles(string id, string declaration)
        {
            IReadOnlyList<Diagnostic> reported = Run(declaration);

            Assert.Contains(reported, d => d.Id == id);

            // The creator is already an error: its members do not also ask for a setter.
            Assert.DoesNotContain(reported, d => d.Id == "CBOR1006");
            AssertCompiles(declaration);
        }

        /// <summary>A protected constructor is reachable from a type nested in a derived class only through base(...).</summary>
        [Fact]
        public void AProtectedConstructorIsNotReachableFromAContextNestedInADerivedType()
        {
            string source = Preamble + @"
public class Holder { [CborConstructor] protected Holder(int id) { Id = id; } public int Id { get; } }

public partial class Derived : Holder
{
    public Derived() : base(0) { }

    [CborSerializable(typeof(Holder))]
    public partial class Context : CborSerializerContext { }
}";

            Assert.Contains(GeneratorHarness.Run(source), d => d.Id == "CBOR1015");
            Assert.Empty(GeneratorHarness.CompileGeneratedOutput(source));
        }

        /// <summary>
        /// A referenced assembly's <c>internal</c> constructor is not imported as a symbol at all, so
        /// it is not a creator and the type is reported as having nothing to build it with.
        /// </summary>
        [Theory]
        [InlineData("internal", "CBOR1010")]
        [InlineData("public", null)]
        public void AnInternalConstructorOfAnotherAssemblyIsNotACreator(string accessibility, string expected)
        {
            (IReadOnlyList<Diagnostic> diagnostics, _) = GeneratorHarness.RunAgainstLibrary(
                Preamble + $"public class Holder {{ [CborConstructor] {accessibility} Holder(int id) {{ Id = id; }} public int Id {{ get; }} }}",
                Preamble + Context);

            Assert.Equal(expected, diagnostics.Select(d => d.Id).FirstOrDefault(id => id == "CBOR1010"));
            Assert.DoesNotContain(diagnostics, d => d.Id == "CBOR1015");
        }
    }
}
