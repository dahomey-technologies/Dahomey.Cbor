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
    /// fixture. <see cref="Run"/> covers the common case -- diagnostics only, no inspection of the
    /// generated sources; <see cref="RunAndGetGeneratedSources"/> is the escape hatch for the rare
    /// assertion that needs to see what the generator actually emitted.
    /// </remarks>
    internal static class CddlGeneratorHarness
    {
        public static ImmutableArray<Diagnostic> Run(string source)
        {
            return RunAndGetGeneratedSources(source).Diagnostics;
        }

        /// <summary>
        /// Same run, but also hands back the text of every source the generator added -- what
        /// <see cref="Run"/> discards -- for the rare assertion that needs to see the emitted CDDL
        /// itself rather than just the diagnostics about it (e.g. that two colliding rule names were
        /// actually disambiguated, not merely that neither raised an error).
        /// </summary>
        public static (ImmutableArray<Diagnostic> Diagnostics, string GeneratedText) RunAndGetGeneratedSources(
            string source)
        {
            CSharpCompilation compilation = Compile(source);

            GeneratorDriver driver = CSharpGeneratorDriver
                .Create(new CborSourceGenerator())
                .RunGeneratorsAndUpdateCompilation(
                    compilation, out _, out ImmutableArray<Diagnostic> diagnostics);

            string generatedText = string.Concat(driver.GetRunResult().Results
                .SelectMany(result => result.GeneratedSources)
                .Select(generated => generated.SourceText.ToString()));

            return (diagnostics, generatedText);
        }

        /// <summary>
        /// Runs the generator and hands back the emitted CDDL itself, decoded out of the verbatim
        /// string literal <see cref="Dahomey.Cbor.Generator.CddlEmitter"/> wraps it in.
        /// </summary>
        /// <remarks>
        /// For fixtures that cannot live in this project as ordinary types. A member declared
        /// <c>List&lt;string?&gt;</c> is the case in point: the registration emitter names member
        /// mapping types by a display format that drops the nullable-reference modifier, so the
        /// generated mapping is <c>DelegateMemberMapping&lt;T, List&lt;string&gt;&gt;</c> and the
        /// consuming compilation raises CS8619 on the accessor lambdas. Real and pre-existing, but not
        /// this branch's to fix and not worth adding a warning to its build for -- so the fixture is
        /// compiled in memory, where its warnings stay in the harness.
        /// </remarks>
        public static string RunAndGetCddlSchema(string source)
        {
            string generated = RunAndGetCddlSource(source);

            // EmitSource writes `public const string CddlSchema =` followed by a verbatim string and
            // nothing else after it, so the last `";` in that one file closes the schema. Doubled
            // quotes are the verbatim literal's own escaping and are undone here.
            int start = generated.IndexOf("@\"", StringComparison.Ordinal) + 2;
            int end = generated.LastIndexOf("\";", StringComparison.Ordinal);

            return generated.Substring(start, end - start).Replace("\"\"", "\"").Replace("\r\n", "\n");
        }

        /// <summary>
        /// The C# of the schema file alone -- the <c>.CddlSchema.g.cs</c> the CDDL emitter adds, not
        /// the converter half <see cref="Emitter"/> adds beside it.
        /// </summary>
        /// <remarks>
        /// Which file the text came from is the whole assertion for anything about how the context is
        /// reopened: the converter half already reopens containing types correctly, so a test that
        /// searched the concatenation of every generated source would pass on its output alone and say
        /// nothing about this one.
        /// </remarks>
        public static string RunAndGetCddlSource(string source)
        {
            GeneratorDriver driver = CSharpGeneratorDriver
                .Create(new CborSourceGenerator())
                .RunGenerators(Compile(source));

            return driver.GetRunResult().Results
                .SelectMany(result => result.GeneratedSources)
                .Where(candidate => candidate.HintName.EndsWith(".CddlSchema.g.cs", StringComparison.Ordinal))
                .Select(candidate => candidate.SourceText.ToString())
                .Single();
        }

        /// <summary>
        /// Runs the generator and hands back the errors the C# compiler raises over the source
        /// <em>plus</em> everything the generator added.
        /// </summary>
        /// <remarks>
        /// <see cref="Run"/> reports what the generator itself says, which is silent about the
        /// failure mode where the emitted code is well-formed on its own but does not agree with the
        /// user's declaration -- a partial reopened with the wrong accessibility (CS0262) or without
        /// its type parameters (CS0264). Those only surface once the two halves are compiled
        /// together, which is what this does.
        /// </remarks>
        public static ImmutableArray<Diagnostic> RunAndGetCompilationErrors(string source)
        {
            CSharpGeneratorDriver
                .Create(new CborSourceGenerator())
                .RunGeneratorsAndUpdateCompilation(Compile(source), out Compilation updated, out _);

            return updated.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .ToImmutableArray();
        }

        private static CSharpCompilation Compile(string source)
        {
            return CSharpCompilation.Create(
                "CddlGeneratorHarness",
                new[] { CSharpSyntaxTree.ParseText(source) },
                References(),
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Annotations));
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
