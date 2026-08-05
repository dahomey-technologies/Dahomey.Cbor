using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dahomey.Cbor.GeneratorTests
{
    /// <summary>
    /// Compiles a source snippet with the generator attached and hands back what it reported.
    /// </summary>
    /// <remarks>
    /// Driving the generator directly is the only way to assert on a diagnostic it is supposed to
    /// produce: attaching it as an analyzer to a test project would make every negative case a build
    /// error, so the cases that matter most could not be written down at all.
    /// </remarks>
    internal static class GeneratorHarness
    {
        /// <summary>Diagnostics reported by the generator itself, in id order.</summary>
        public static IReadOnlyList<Diagnostic> Run(string source)
        {
            Run(source, out ImmutableArray<Diagnostic> generatorDiagnostics, out _);
            return generatorDiagnostics.OrderBy(diagnostic => diagnostic.Id).ToList();
        }

        /// <summary>
        /// Errors from compiling the generator's own output, which is how an emission bug shows up:
        /// CS0262 for a mismatched accessibility, CS0534 for a context whose Configure never landed.
        /// </summary>
        public static IReadOnlyList<Diagnostic> CompileGeneratedOutput(string source)
        {
            Run(source, out _, out Compilation output);

            return output.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToList();
        }

        public static string GeneratedSource(string source)
        {
            Run(source, out _, out Compilation output);

            return string.Join(
                Environment.NewLine,
                output.SyntaxTrees.Skip(1).Select(tree => tree.ToString()));
        }

        private static void Run(
            string source,
            out ImmutableArray<Diagnostic> generatorDiagnostics,
            out Compilation output)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "GeneratorTestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                ReferenceSet(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            CSharpGeneratorDriver.Create(new CborSourceGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out output, out generatorDiagnostics);
        }

        /// <summary>
        /// Everything already loaded, plus Dahomey.Cbor itself. Enumerating the load context keeps
        /// this working across the three target frameworks without pinning reference assembly paths.
        /// </summary>
        private static IEnumerable<MetadataReference> ReferenceSet()
        {
            HashSet<string> locations = new HashSet<string>(StringComparer.Ordinal);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                {
                    locations.Add(assembly.Location);
                }
            }

            locations.Add(typeof(CborSerializableAttribute).Assembly.Location);
            locations.Add(typeof(object).Assembly.Location);

            return locations.Select(location => (MetadataReference)MetadataReference.CreateFromFile(location));
        }
    }
}
