using Dahomey.Cbor.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// Runs the source generator over a source string in memory and hands back what it reported.
    /// </summary>
    /// <remarks>
    /// The only way to assert that a diagnostic <em>fires</em>: a diagnostic raised over this test
    /// project's own sources is a build error, so the failing case cannot be written as an ordinary
    /// fixture. Deliberately minimal -- diagnostics only, no inspection of the generated sources.
    /// </remarks>
    internal static class CddlGeneratorHarness
    {
        public static ImmutableArray<Diagnostic> Run(string source)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "CddlGeneratorHarness",
                new[] { CSharpSyntaxTree.ParseText(source) },
                References(),
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Annotations));

            CSharpGeneratorDriver
                .Create(new CborSourceGenerator())
                .RunGeneratorsAndUpdateCompilation(
                    compilation, out _, out ImmutableArray<Diagnostic> diagnostics);

            return diagnostics;
        }

        /// <summary>
        /// The running host's trusted platform assemblies, plus Dahomey.Cbor itself. Taken from the
        /// process rather than assembled by hand so the compilation sees exactly the framework the tests
        /// run against, and so a reference added to the test project needs no change here.
        /// </summary>
        private static IEnumerable<MetadataReference> References()
        {
            string platform = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");

            return platform
                .Split(Path.PathSeparator)
                .Concat(new[] { typeof(Attributes.CborSerializableAttribute).Assembly.Location })
                .Where(path => path.Length > 0)
                .Distinct()
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
        }
    }
}
