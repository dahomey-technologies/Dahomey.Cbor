using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// The CDDL schema is a second partial declaration of the user's own context, so it has to reopen
    /// that context exactly as it was declared. These cases cannot be written as ordinary fixtures:
    /// each one's symptom is that the test project stops compiling.
    /// </summary>
    public class CddlContextDeclarationTests
    {
        private const string Preamble = @"
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;

namespace Harness
{
";

        /// <summary>
        /// A hardcoded <c>public</c> against an internal context is CS0262 -- and since the attribute
        /// is opt-in on a context that already compiles, that is a working project broken by adding
        /// it.
        /// </summary>
        [Fact]
        public void InternalContextIsReopenedWithItsDeclaredAccessibility()
        {
            ImmutableArray<Diagnostic> errors = CddlGeneratorHarness.RunAndGetCompilationErrors(Preamble + @"
    public class Person { public int Age { get; set; } }

    [CborSerializable(typeof(Person))]
    [CborCddlSchema]
    internal partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.Empty(errors);
        }

        /// <summary>
        /// A nested context needs every containing type reopened, or the schema half lands as a new
        /// top-level type: no error, and <c>Outer.HarnessContext.CddlSchema</c> simply does not exist.
        /// Asserting on the emitted text as well as on the absence of errors is what catches that,
        /// since a stray top-level type compiles perfectly well.
        /// </summary>
        [Fact]
        public void NestedContextIsReopenedInsideItsContainingType()
        {
            const string Source = Preamble + @"
    public class Person { public int Age { get; set; } }

    public partial class Outer
    {
        [CborSerializable(typeof(Person))]
        [CborCddlSchema]
        public partial class HarnessContext : CborSerializerContext { }
    }
}
";

            Assert.Empty(CddlGeneratorHarness.RunAndGetCompilationErrors(Source));

            // The schema file specifically, not every generated source: the converter half reopens
            // Outer correctly on its own, so searching the concatenation would pass either way.
            Assert.Contains("partial class Outer", CddlGeneratorHarness.RunAndGetCddlSource(Source));
        }

        /// <summary>
        /// A generic context reopened without its type parameters is CS0264. Asserted on the emitted
        /// text rather than on compiler errors: a generic context is unusable for other reasons -- the
        /// generator's own <c>Default&lt;T&gt;</c> entry point takes a closed type -- so the CS0264 the
        /// hardcoded name produced is not the only thing wrong with this shape, and pinning the text is
        /// what keeps the assertion about this file.
        /// </summary>
        [Fact]
        public void GenericContextIsReopenedWithItsTypeParameters()
        {
            string generated = CddlGeneratorHarness.RunAndGetCddlSource(Preamble + @"
    public class Person { public int Age { get; set; } }

    [CborSerializable(typeof(Person))]
    [CborCddlSchema]
    public partial class HarnessContext<T> : CborSerializerContext { }
}
");

            Assert.Contains("partial class HarnessContext<T>", generated);
        }

        /// <summary>
        /// The ordinary case, so that an assertion of "no errors" above is worth something: the same
        /// harness over a plain public top-level context has to be clean too.
        /// </summary>
        [Fact]
        public void PublicTopLevelContextStillCompiles()
        {
            ImmutableArray<Diagnostic> errors = CddlGeneratorHarness.RunAndGetCompilationErrors(Preamble + @"
    public class Person { public int Age { get; set; } }

    [CborSerializable(typeof(Person))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.Empty(errors);
        }
    }
}
