using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
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

    /// <summary>The same, with one escape, and the pair below asks for one rule name.</summary>
    public class Café
    {
        public int Value { get; set; }
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
    /// and it reads a file whose second definition of a rule shadows the first without saying anything
    /// at all.
    /// </summary>
    /// <remarks>
    /// The second is the failure worth having a test for, because it is the one that stays quiet. A
    /// schema whose rules collide describes something other than the types it was generated from, and
    /// every instance check run against it passes or fails on the surviving rule.
    /// </remarks>
    public class CddlIdentifierSyntaxTests
    {
        [Fact]
        public void EveryCharacterOutsideAsciiIsEscapedInARuleName()
        {
            string schema = CddlIdentifierSyntaxContext.CddlSchema.Replace("\r\n", "\n");

            Assert.DoesNotContain("ö", schema);
            Assert.DoesNotContain("ß", schema);
            Assert.Contains("Gr_00F6_00DFe = {\n", schema);
        }

        /// <summary>
        /// Which of the two keeps the bare name is settled by ordinal order of the fully qualified type
        /// name, so it is a function of the types alone rather than of the order the generator collected
        /// them in — two builds of one context have to agree, since a schema is compared against other
        /// copies of itself. <c>_</c> sorts below <c>é</c>, so the type spelling the escape out keeps it.
        /// </summary>
        [Fact]
        public void TwoTypesAskingForOneRuleNameEachGetTheirOwn()
        {
            string schema = CddlIdentifierSyntaxContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("Caf_00E9 = {\n", schema);
            Assert.Contains("Caf_00E9-2 = {\n", schema);
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
