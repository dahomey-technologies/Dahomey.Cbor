#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
            if (_path is null)
            {
                throw new InvalidOperationException(
                    "The cddl gem is not installed. Run: gem install --user-install cddl");
            }

            string directory = Path.Combine(Path.GetTempPath(), "dahomey-cddl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                string schemaPath = Path.Combine(directory, "schema.cddl");
                string cborPath = Path.Combine(directory, "instance.cbor");

                File.WriteAllText(schemaPath, "start = " + rule + "\n\n" + schema + "\n");
                File.WriteAllBytes(cborPath, cbor);

                ProcessStartInfo startInfo = new ProcessStartInfo(_path)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                startInfo.ArgumentList.Add(schemaPath);
                startInfo.ArgumentList.Add("validate");
                startInfo.ArgumentList.Add(cborPath);

                using Process process = Process.Start(startInfo)!;
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();

                return new CddlResult(process.ExitCode == 0, output);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
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
