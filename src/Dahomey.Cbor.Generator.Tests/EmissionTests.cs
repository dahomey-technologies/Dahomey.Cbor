using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dahomey.Cbor.GeneratorTests
{
    /// <summary>
    /// Shapes of context declaration that are legal C# and used to make the generated half fail to
    /// compile, or the generator itself throw.
    /// </summary>
    /// <remarks>
    /// These do not assert on generator diagnostics — they compile the generated output, because that
    /// is where the failure landed: CS0262 for an accessibility that does not match, CS0534 for a
    /// context whose <c>Configure</c> never arrived, and a duplicate hint name for two contexts that
    /// share a simple name.
    /// </remarks>
    public class EmissionTests
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
";

        private static void AssertCompiles(string body)
        {
            IReadOnlyList<Diagnostic> errors = GeneratorHarness.CompileGeneratedOutput(Preamble + body);

            Assert.True(
                errors.Count == 0,
                "generated output did not compile: "
                    + string.Join(", ", errors.Select(e => e.Id + ": " + e.GetMessage())));
        }

        [Fact]
        public void APublicContextInANamespaceCompiles()
        {
            AssertCompiles(@"
namespace App
{
    public class Person { public int Id { get; set; } }

    [CborSerializable(typeof(Person))]
    public partial class Context : CborSerializerContext { }
}
");
        }

        /// <summary>
        /// Accessibility was hardcoded to <c>public</c>, so an internal context was CS0262: partial
        /// declarations have conflicting accessibility modifiers.
        /// </summary>
        [Fact]
        public void AnInternalContextCompiles()
        {
            AssertCompiles(@"
namespace App
{
    public class Person { public int Id { get; set; } }

    [CborSerializable(typeof(Person))]
    internal partial class Context : CborSerializerContext { }
}
");
        }

        /// <summary>
        /// Containing types were not walked, so the generated half landed as a top-level type and the
        /// user's nested class was left abstract: CS0534, naming nothing to do with generators.
        /// </summary>
        [Fact]
        public void ANestedContextCompiles()
        {
            AssertCompiles(@"
namespace App
{
    public class Person { public int Id { get; set; } }

    public partial class Outer
    {
        [CborSerializable(typeof(Person))]
        public partial class Context : CborSerializerContext { }
    }
}
");
        }

        [Fact]
        public void ADoublyNestedContextCompiles()
        {
            AssertCompiles(@"
namespace App
{
    public class Person { public int Id { get; set; } }

    internal partial class Outer
    {
        public partial class Middle
        {
            [CborSerializable(typeof(Person))]
            internal partial class Context : CborSerializerContext { }
        }
    }
}
");
        }

        /// <summary>
        /// The hint name was the simple type name, so two contexts called the same thing in different
        /// namespaces collided and the generator threw.
        /// </summary>
        [Fact]
        public void TwoContextsWithTheSameNameInDifferentNamespacesCompile()
        {
            AssertCompiles(@"
namespace One
{
    public class Person { public int Id { get; set; } }

    [CborSerializable(typeof(Person))]
    public partial class Context : CborSerializerContext { }
}

namespace Two
{
    public class Address { public string City { get; set; } }

    [CborSerializable(typeof(Address))]
    public partial class Context : CborSerializerContext { }
}
");
        }

        /// <summary>
        /// Two roots with the same simple name in different namespaces produced two accessors with
        /// the same identifier: CS0102.
        /// </summary>
        [Fact]
        public void TwoRootsWithTheSameNameInDifferentNamespacesCompile()
        {
            AssertCompiles(@"
namespace One { public class Person { public int Id { get; set; } } }
namespace Two { public class Person { public string Name { get; set; } } }

namespace App
{
    [CborSerializable(typeof(One.Person))]
    [CborSerializable(typeof(Two.Person))]
    public partial class Context : CborSerializerContext { }
}
");
        }

        /// <summary>
        /// The naming convention has to reach the reflection fallback too, or a document written
        /// through a context ends up with two naming conventions in it.
        /// </summary>
        [Fact]
        public void ANamingConventionIsAppliedToTheOptions()
        {
            string generated = GeneratorHarness.GeneratedSource(Preamble + @"
namespace App
{
    public class Person { public int SomeId { get; set; } }

    [CborSerializable(typeof(Person))]
    [CborSourceGenerationOptions(NamingConvention = typeof(Dahomey.Cbor.Serialization.Conventions.CamelCaseNamingConvention))]
    public partial class Context : CborSerializerContext { }
}
");

            Assert.Contains("options.DefaultNamingConvention", generated);
            Assert.Contains("someId", generated);
        }
    }
}
