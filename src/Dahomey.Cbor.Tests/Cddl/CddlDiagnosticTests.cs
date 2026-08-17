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

            Diagnostic reported = Assert.Single(diagnostics, d => d.Id == "CBOR1012");

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

            Assert.Empty(diagnostics.Where(d => d.Id == "CBOR1012"));
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

        /// <summary>
        /// In its default encoding System.Decimal is 0xFC plus 16 bytes, where additional information
        /// 28 is reserved and ill-formed under RFC 8949 section 3 -- no conforming decoder can read it,
        /// so no CDDL can describe it. <see cref="CddlTypeReference"/>'s primitive case returns nothing
        /// for it, which is what CBOR1011 exists to catch rather than silently omitting the member.
        /// </summary>
        [Fact]
        public void DecimalHasNoCddlRepresentation()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Money { public decimal Amount { get; set; } }

    [CborSerializable(typeof(Money))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Diagnostic reported = Assert.Single(diagnostics, d => d.Id == "CBOR1011");
            Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        }

        /// <summary>
        /// Declare the interoperable encoding and the same member is describable: a decimal fraction is
        /// tag 4 over an exponent and a mantissa, all of which CDDL has words for. This is the one
        /// diagnostic in this file a setting can turn off, which is the point -- the schema follows what
        /// the context writes.
        /// </summary>
        [Fact]
        public void DecimalHasACddlRepresentationAsADecimalFraction()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Money { public decimal Amount { get; set; } }

    [CborSerializable(typeof(Money))]
    [CborSourceGenerationOptions(DecimalFormat = Dahomey.Cbor.DecimalFormat.DecimalFraction)]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.DoesNotContain(diagnostics, d => d.Id == "CBOR1011");
        }

        /// <summary>
        /// Guid does render: GuidConverter writes RFC 9562's binary UUID, tag 37 over sixteen bytes.
        /// Kept here because it sat beside the decimal above as a refusal, and which of the two still
        /// is belongs in one place.
        /// </summary>
        [Fact]
        public void GuidHasACddlRepresentation()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Keyed { public System.Guid Key { get; set; } }

    [CborSerializable(typeof(Keyed))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.DoesNotContain(diagnostics, d => d.Id == "CBOR1011");
        }

        // char and BigInteger are deliberately absent from this file: both have concrete converters
        // (CharConverter and BigIntegerConverter), so both render rather than raising CBOR1011.
        // CddlScalarMappingTests pins what they render as.

        /// <summary>
        /// DateTimeOffset has no scalar converter either -- only System.DateTime does, via
        /// DateTimeConverter.
        /// </summary>
        [Fact]
        public void DateTimeOffsetHasNoCddlRepresentation()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Logged { public System.DateTimeOffset When { get; set; } }

    [CborSerializable(typeof(Logged))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Diagnostic reported = Assert.Single(diagnostics, d => d.Id == "CBOR1011");
            Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        }

        /// <summary>
        /// Scalar System.Half has no converter either -- the only place the library references it is the
        /// RFC 8746 typed-array element path, a different representation entirely (`#6.84(bstr)`), not a
        /// scalar member's.
        /// </summary>
        [Fact]
        public void ScalarHalfHasNoCddlRepresentation()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Measurement { public System.Half Value { get; set; } }

    [CborSerializable(typeof(Measurement))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Diagnostic reported = Assert.Single(diagnostics, d => d.Id == "CBOR1011");
            Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        }

        /// <summary>
        /// A declared root that is a collection gets a rule of its own, so a root whose element has no
        /// representation has to raise CBOR1011 rather than emit nothing. Neither the collector nor the
        /// member walk would catch this one: <c>decimal</c> classifies as a primitive, so it draws no
        /// CBOR1002, and the root has no member above it to report against.
        /// </summary>
        [Fact]
        public void ARootCollectionOfAnUnrepresentableElementIsAnError()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    [CborSerializable(typeof(System.Collections.Generic.List<decimal>))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Diagnostic reported = Assert.Single(diagnostics, d => d.Id == "CBOR1011");

            Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
            Assert.Contains("ListOfDecimal", reported.GetMessage());
        }

        /// <summary>
        /// A context with no [CborCddlSchema] never runs the CDDL half of the generator at all, so
        /// opting out is genuinely free -- even a member type with no CDDL representation raises nothing.
        /// </summary>
        [Fact]
        public void NoSchemaAttributeMeansNoCddlDiagnostics()
        {
            ImmutableArray<Diagnostic> diagnostics = CddlGeneratorHarness.Run(Preamble + @"
    public class Money { public decimal Amount { get; set; } }

    [CborSerializable(typeof(Money))]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.DoesNotContain(diagnostics, d => d.Id == "CBOR1011");
        }

        /// <summary>
        /// Two types named "Node" in different namespaces must both get a rule -- and the result must
        /// not depend on which the collector happened to see first. Asserting no errors would also pass
        /// if both collapsed onto one rule, so this checks the two distinct, qualified rule names are
        /// actually present in what the generator emitted.
        /// </summary>
        [Fact]
        public void CollidingSimpleNamesAreQualifiedByNamespace()
        {
            (ImmutableArray<Diagnostic> diagnostics, string generatedText) =
                CddlGeneratorHarness.RunAndGetGeneratedSources(Preamble + @"
    namespace Left { public class Node { public int Id { get; set; } } }
    namespace Right { public class Node { public string Name { get; set; } } }

    public class Pair
    {
        public Left.Node First { get; set; }
        public Right.Node Second { get; set; }
    }

    [CborSerializable(typeof(Pair))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
");

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("Harness-Left-Node", generatedText);
            Assert.Contains("Harness-Right-Node", generatedText);
        }
    }
}
