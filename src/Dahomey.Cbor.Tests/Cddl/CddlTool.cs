#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Dahomey.Cbor.Tests.Cddl
{
    public readonly struct CddlResult
    {
        public CddlResult(bool ok, string output)
        {
            Ok = ok;
            Output = output;
        }

        public bool Ok { get; }

        /// <summary>Combined stdout and stderr, for use as an assertion message.</summary>
        public string Output { get; }
    }

    /// <summary>
    /// Runs the Ruby <c>cddl</c> gem, the reference RFC 8610 implementation and the only one available
    /// to .NET. Install it with:
    /// <code>
    /// sudo apt-get install -y ruby-full build-essential
    /// gem install --user-install cddl
    /// </code>
    /// </summary>
    public static class CddlTool
    {
        private static readonly string? _path = Locate();

        /// <summary>
        /// True when the gem is installed. When CDDL_REQUIRED=1 this is forced true so a missing gem
        /// fails the run instead of quietly skipping it -- CI sets that variable, so CI can never stop
        /// checking without anyone noticing.
        /// </summary>
        public static bool Available =>
            _path is not null || Environment.GetEnvironmentVariable("CDDL_REQUIRED") == "1";

        /// <summary>
        /// Validates <paramref name="cbor"/> against <paramref name="rule"/>.
        /// </summary>
        /// <remarks>
        /// The gem matches an instance against the FIRST rule in the file, so the requested rule is
        /// prepended as <c>start</c>. A synthetic choice over every rule would let a document pass by
        /// matching an unrelated one, which would silently defeat the negative tests.
        /// </remarks>
        public static CddlResult Validate(string schema, string rule, byte[] cbor)
        {
            string directory = NewWorkingDirectory();

            try
            {
                string schemaPath = Path.Combine(directory, "schema.cddl");
                string cborPath = Path.Combine(directory, "instance.cbor");

                File.WriteAllText(schemaPath, "start = " + rule + "\n\n" + schema + "\n");
                File.WriteAllBytes(cborPath, cbor);

                (int exitCode, string output) = Run(schemaPath, "validate", cborPath);

                // cddl exits 0 when the instance matches and 1 when it does not. Anything else is
                // the tool failing, which must not read as a rejection -- that would let a negative
                // test pass because Ruby crashed rather than because the schema did its job. Exit 65
                // in particular means the schema did not parse, which Parse is there to test for.
                if (exitCode > 1)
                {
                    throw new InvalidOperationException($"cddl exited with {exitCode}: {output}");
                }

                return new CddlResult(exitCode == 0, output);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// Asks the gem to parse <paramref name="schema"/> and nothing else: <c>Ok</c> is true when the
        /// text is grammatical RFC 8610, false when it is a parse error.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Validate"/> rather than folded into it, because the two answer
        /// different questions and <see cref="Validate"/> deliberately treats a non-parse as a tool
        /// failure. <c>generate</c> is the gem's only parse-without-an-instance mode; it parses the
        /// file, then synthesises a matching document from the first rule and prints it, which is
        /// discarded here. It exits 0 once the file parses and 65 when it does not -- rules unreachable
        /// from the first are only an "*** Unused rule" line on stderr, still exit 0.
        /// </remarks>
        public static CddlResult Parse(string schema)
        {
            string directory = NewWorkingDirectory();

            try
            {
                string schemaPath = Path.Combine(directory, "schema.cddl");

                File.WriteAllText(schemaPath, schema);

                (int exitCode, string output) = Run(schemaPath, "generate");

                // 65 is EX_DATAERR, which the gem uses for a parse error and for nothing else here.
                // Any other non-zero code is the tool failing and must not read as "did not parse".
                if (exitCode != 0 && exitCode != 65)
                {
                    throw new InvalidOperationException($"cddl exited with {exitCode}: {output}");
                }

                return new CddlResult(exitCode == 0, output);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string NewWorkingDirectory()
        {
            if (_path is null)
            {
                throw new InvalidOperationException(
                    "The cddl gem is not installed. Run: gem install --user-install cddl");
            }

            string directory = Path.Combine(Path.GetTempPath(), "dahomey-cddl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static (int ExitCode, string Output) Run(params string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(_path!)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new Process { StartInfo = startInfo };

            // Both streams are drained concurrently: reading one to EOF before starting the other
            // deadlocks as soon as the child fills the unread pipe's buffer, and both a rejected
            // document and a generated one can be arbitrarily long. The event-based readers do that
            // without blocking on a task, and WaitForExit() with no argument waits for the readers to
            // reach EOF as well as for the process itself, so the output is complete when it returns.
            StringBuilder output = new StringBuilder();

            void Append(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    lock (output)
                    {
                        output.AppendLine(e.Data);
                    }
                }
            }

            process.OutputDataReceived += Append;
            process.ErrorDataReceived += Append;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            return (process.ExitCode, output.ToString());
        }

        /// <summary>
        /// Looks on PATH, then in the user-local gem bin directories, since a user-install gem is not
        /// on PATH unless the shell profile puts it there and the test host inherits that profile.
        /// </summary>
        private static string? Locate()
        {
            foreach (string candidate in Candidates())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> Candidates()
        {
            string? path = Environment.GetEnvironmentVariable("PATH");

            if (path is not null)
            {
                foreach (string directory in path.Split(Path.PathSeparator))
                {
                    if (directory.Length > 0)
                    {
                        yield return Path.Combine(directory, "cddl");
                    }
                }
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string gemRoot = Path.Combine(home, ".local", "share", "gem", "ruby");

            if (Directory.Exists(gemRoot))
            {
                foreach (string version in Directory.GetDirectories(gemRoot))
                {
                    yield return Path.Combine(version, "bin", "cddl");
                }
            }
        }
    }
}
