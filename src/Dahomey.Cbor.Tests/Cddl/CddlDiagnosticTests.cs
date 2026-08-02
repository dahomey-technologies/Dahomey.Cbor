using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// The firing half of the CDDL diagnostics. Every other test in this folder can only show that a
    /// diagnostic stays silent -- a fixture that triggers one would not compile.
    /// </summary>
    public class CddlDiagnosticTests
    {
        private const string Preamble = @"
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;

namespace Harness
{
";

        /// <summary>
        /// An abstract base is described only by its subtypes' arms. When none of them carries a
        /// discriminator the choice describes a document nothing can tell apart, which is the quiet
        /// failure the schema exists to prevent -- so it is an error rather than a silently weak schema.
        /// </summary>
        [Fact]
        public void UndiscriminatedPolymorphicBaseIsAnError()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public abstract class Shape { public int Id { get; set; } }
    public class Circle : Shape { public double Radius { get; set; } }
    public class Square : Shape { public double Side { get; set; } }

    public class Drawing { public Shape Shape { get; set; } }

    [CborSerializable(typeof(Drawing))]
    [CborSerializable(typeof(Circle))]
    [CborSerializable(typeof(Square))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Diagnostic reported = Assert.Single(diagnostics, d => d.Id == "CBOR1008");

            Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
            Assert.Contains("Harness.Shape", reported.GetMessage());
        }

        /// <summary>
        /// Give the same hierarchy discriminators and it goes quiet: the diagnostic is about a choice
        /// with nothing to distinguish its arms, not about polymorphism.
        /// </summary>
        [Fact]
        public void DiscriminatedPolymorphicBaseIsSilent()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public abstract class Shape { public int Id { get; set; } }

    [CborDiscriminator(""circle"")]
    public class Circle : Shape { public double Radius { get; set; } }

    [CborDiscriminator(""square"")]
    public class Square : Shape { public double Side { get; set; } }

    public class Drawing { public Shape Shape { get; set; } }

    [CborSerializable(typeof(Drawing))]
    [CborSerializable(typeof(Circle))]
    [CborSerializable(typeof(Square))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.Empty(diagnostics.Where(d => d.Id == "CBOR1008"));
        }

        /// <summary>
        /// The false positive that would matter most: an ordinary concrete class with no subtypes is the
        /// overwhelmingly common shape, and it carries no discriminator either.
        /// </summary>
        [Fact]
        public void OrdinaryConcreteClassIsSilent()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Person { public string Name { get; set; } public int Age { get; set; } }

    [CborSerializable(typeof(Person))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.Empty(diagnostics);
        }
    }
}
