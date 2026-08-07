using System.Collections.Generic;
using System.Linq;
using Dahomey.Cbor.Generator;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dahomey.Cbor.GeneratorTests
{
    /// <summary>
    /// What the pipeline does on the second keystroke. A source generator runs on every edit in the
    /// IDE, so the cost that matters is not the first run but each one after it.
    /// </summary>
    /// <remarks>
    /// The two halves are in tension and both are pinned here. Reusing too little makes every
    /// unrelated edit pay for the whole generation; reusing too much emits registrations for a shape
    /// the code no longer has, which is worse, because it compiles.
    /// </remarks>
    public class IncrementalPipelineTests
    {
        private const string Preamble = @"
using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
";

        private const string ContextFile = Preamble + @"
namespace Sample
{
    [CborSerializable(typeof(Person))]
    public partial class SampleContext : CborSerializerContext { }
}
";

        private const string PersonFile = Preamble + @"
namespace Sample
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
";

        private const string UnrelatedFile = @"
namespace Sample
{
    public class Unrelated
    {
        public int Value { get; set; }
    }
}
";

        /// <summary>
        /// The whole point: editing a file that has nothing to do with any context must not produce a
        /// new generated source. Adding a syntax tree is what re-triggers everything downstream of the
        /// generator, so an output that is merely recomputed and found equal costs nothing, while one
        /// that is re-added costs a re-analysis of the compilation.
        /// </summary>
        [Fact]
        public void AnEditToAnUnrelatedFileProducesNoNewOutput()
        {
            (_, GeneratorDriverRunResult second) = GeneratorHarness.RunAfterEditing(
                new[] { ContextFile, PersonFile, UnrelatedFile },
                indexToEdit: 2,
                replacement: UnrelatedFile.Replace("int Value", "long Value"));

            Assert.All(
                GeneratorHarness.StepReasons(second, CborSourceGenerator.GeneratedContextsStep),
                reason => Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"the generated context was {reason}, so an unrelated edit regenerated it"));

            Assert.All(
                GeneratorHarness.StepReasons(second, "SourceOutput"),
                reason => Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"source output was {reason}, so an unrelated edit re-added the generated tree"));
        }

        /// <summary>
        /// Nothing in the compilation carries a discriminator, so the step that feeds CBOR1009 has
        /// nothing to recompute. It used to walk every type in the assembly to establish that.
        /// </summary>
        [Fact]
        public void AnEditToAnUnrelatedFileDoesNotRescanForDiscriminatedTypes()
        {
            (_, GeneratorDriverRunResult second) = GeneratorHarness.RunAfterEditing(
                new[] { ContextFile, PersonFile, UnrelatedFile },
                indexToEdit: 2,
                replacement: UnrelatedFile.Replace("int Value", "long Value"));

            Assert.All(
                GeneratorHarness.StepReasons(second, CborSourceGenerator.DiscriminatedTypesStep),
                reason => Assert.True(
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"the discriminated-type index was {reason}"));
        }

        /// <summary>
        /// The other half, and the reason the generation step takes the compilation rather than
        /// trusting the context file's own semantic model: a declared type's members live in a
        /// different file, and a pipeline keyed on the context's syntax alone would reuse its previous
        /// answer and emit a mapping for a member that no longer exists.
        /// </summary>
        [Fact]
        public void AddingAMemberToADeclaredTypeInAnotherFileRegenerates()
        {
            (GeneratorDriverRunResult first, GeneratorDriverRunResult second) =
                GeneratorHarness.RunAfterEditing(
                    new[] { ContextFile, PersonFile, UnrelatedFile },
                    indexToEdit: 1,
                    replacement: PersonFile.Replace(
                        "public string Name { get; set; }",
                        "public string Name { get; set; } public bool Active { get; set; }"));

            Assert.DoesNotContain("Active", GeneratorHarness.GeneratedSourceOf(first));
            Assert.Contains("Active", GeneratorHarness.GeneratedSourceOf(second));
        }

        /// <summary>
        /// Same staleness question for a diagnostic rather than for emitted code: a subtype declared
        /// after the context was last touched still has to be reported.
        /// </summary>
        [Fact]
        public void ASubtypeAddedInAnotherFileIsReported()
        {
            const string baseFile = Preamble + @"
namespace Sample
{
    [CborDiscriminator(""base"")]
    public class Shape { public int Id { get; set; } }
}
";

            const string contextFile = Preamble + @"
namespace Sample
{
    [CborSerializable(typeof(Shape))]
    public partial class ShapeContext : CborSerializerContext { }
}
";

            (GeneratorDriverRunResult first, GeneratorDriverRunResult second) =
                GeneratorHarness.RunAfterEditing(
                    new[] { contextFile, baseFile, UnrelatedFile },
                    indexToEdit: 2,
                    replacement: Preamble + @"
namespace Sample
{
    [CborDiscriminator(""circle"")]
    public class Circle : Shape { public int Radius { get; set; } }
}
");

            Assert.DoesNotContain(Ids(first), id => id == "CBOR1009");
            Assert.Contains(Ids(second), id => id == "CBOR1009");
        }

        private static IReadOnlyList<string> Ids(GeneratorDriverRunResult result)
        {
            return result.Diagnostics.Select(diagnostic => diagnostic.Id).ToList();
        }
    }
}
