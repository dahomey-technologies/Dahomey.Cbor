using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// A type whose name is a perfectly ordinary C# identifier and not an RFC 8610 one. Any Unicode
    /// letter is legal in an identifier; <c>id</c> is ASCII-only, so the name reaches the schema as an
    /// escape — two of them here, one per character outside ASCII.
    /// </summary>
    public class Größe
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// The same, with one escape, and the pair below asks for one rule name. Its member differs from
    /// <see cref="Caf_00E9"/>'s deliberately: two rules with identical bodies are indistinguishable in
    /// the emitted schema, so which of the pair keeps the bare name would be unassertable, and the gem
    /// would take a duplicate between them as a warning rather than an error.
    /// </summary>
    public class Café
    {
        public string Name { get; set; }
    }

    /// <summary>
    /// The name <see cref="Café"/> escapes to, declared literally. Contrived on purpose: it is the one
    /// shape that makes two distinct types ask for one rule name, because escaping folds a character
    /// outside ASCII onto a <c>_</c> sequence that an identifier is free to spell out.
    /// </summary>
    public class Caf_00E9
    {
        public int Value { get; set; }
    }

    [CborSerializable(typeof(Größe))]
    [CborSerializable(typeof(Café))]
    [CborSerializable(typeof(Caf_00E9))]
    [CborCddlSchema]
    public partial class CddlIdentifierSyntaxContext : CborSerializerContext
    {
    }

    /// <summary>
    /// A rule name has to be an RFC 8610 <c>id</c>, and no two rules in one schema may share one.
    /// Neither is a preference: the gem stops on a non-ASCII rule name with a parse error and exit 65,
    /// and what it does with a duplicate depends on the two bodies — an identical redefinition is a
    /// warning on stderr and exit 0, differing bodies a <c>RuntimeError</c> and exit 1.
    /// </summary>
    /// <remarks>
    /// The identical case is the failure worth having a test for, because it is the one that stays
    /// quiet: a schema whose rules collide describes something other than the types it was generated
    /// from, and every instance check run against it passes or fails on the surviving rule.
    /// </remarks>
    public class CddlIdentifierSyntaxTests
    {
        /// <summary>
        /// Asserted over the rule names rather than the whole schema: CDDL text literals accept
        /// <c>NONASCII</c>, so a member named <c>Grüße</c> reaches the output as itself and is no
        /// evidence either way.
        /// </summary>
        [Fact]
        public void EveryCharacterOutsideAsciiIsEscapedInARuleName()
        {
            string schema = CddlIdentifierSyntaxContext.CddlSchema.Replace("\r\n", "\n");

            foreach (string ruleName in RuleNames(schema))
            {
                Assert.All(ruleName, character => Assert.InRange(character, (char)0x20, (char)0x7E));
            }

            Assert.Contains("Gr_00F6_00DFe = {\n", schema);
        }

        /// <summary>
        /// Which of the two keeps the bare name is settled by ordinal order of the fully qualified type
        /// name, so it is a function of the types alone rather than of the order the generator collected
        /// them in — two builds of one context have to agree, since a schema is compared against other
        /// copies of itself. <c>_</c> sorts below <c>é</c>, so the type spelling the escape out keeps it.
        /// </summary>
        /// <remarks>
        /// The bodies say which is which. Asserting only that both names are present would pass with the
        /// tie-break reversed, which is the one regression the ordinal ordering exists to prevent.
        /// </remarks>
        [Fact]
        public void TwoTypesAskingForOneRuleNameEachGetTheirOwn()
        {
            string schema = CddlIdentifierSyntaxContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("Caf_00E9 = {\n  \"Value\"", schema);
            Assert.Contains("Caf_00E9-2 = {\n  \"Name\"", schema);
        }

        /// <summary>The left-hand side of every rule in the schema.</summary>
        /// <remarks>
        /// A comment is excluded by its leading <c>;</c> rather than by the assumption that no comment
        /// contains <c>" = "</c>. None does today, so this changes nothing now; it stops the day a header
        /// line gains one, at which point the comment would be scanned as a rule name and the assertion
        /// would fail for a reason that has nothing to do with rule names.
        /// </remarks>
        private static IEnumerable<string> RuleNames(string schema)
        {
            foreach (string line in schema.Split('\n'))
            {
                if (line.StartsWith(" ", StringComparison.Ordinal)
                    || line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                int assignment = line.IndexOf(" = ", StringComparison.Ordinal);

                if (assignment > 0)
                {
                    yield return line.Substring(0, assignment);
                }
            }
        }

        /// <summary>
        /// The escape and the suffix are only worth anything if the result parses.
        /// <c>CddlSchemaParsesTests</c> covers this context too, by assembly scan; this says so where
        /// the reason lives, since every name asserted above is constructed rather than copied from a
        /// type.
        /// </summary>
        [CddlFact]
        public void TheEscapedAndSuffixedSchemaParses()
        {
            CddlResult result = CddlTool.Parse(CddlIdentifierSyntaxContext.CddlSchema);

            Assert.True(result.Ok, result.Output);
        }
    }
}
