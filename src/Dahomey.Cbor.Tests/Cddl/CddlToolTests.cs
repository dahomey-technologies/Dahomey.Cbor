using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// Checks the bridge to the reference implementation against hand-written schemas, so a failure
    /// here is unambiguously the harness rather than the emitter.
    /// </summary>
    public class CddlToolTests
    {
        [CddlFact]
        public void AcceptsAMatchingDocument()
        {
            // 0x83 0x01 0x02 0x03 -- [1, 2, 3]
            byte[] cbor = new byte[] { 0x83, 0x01, 0x02, 0x03 };

            CddlResult result = CddlTool.Validate("Triple = [int, int, int]", "Triple", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void RejectsAMismatchingDocument()
        {
            // 0x83 0x01 0x02 0x63 0x66 0x6F 0x6F -- [1, 2, "foo"]
            byte[] cbor = new byte[] { 0x83, 0x01, 0x02, 0x63, 0x66, 0x6F, 0x6F };

            CddlResult result = CddlTool.Validate("Triple = [int, int, int]", "Triple", cbor);

            Assert.False(result.Ok);
        }

        /// <summary>
        /// The gem validates against the first rule in the file, so the harness must target a rule by
        /// name. Without that, a document could pass by matching some unrelated rule and every
        /// negative test in this suite would be vacuous.
        /// </summary>
        [CddlFact]
        public void TargetsTheNamedRuleRatherThanTheFirstOne()
        {
            byte[] cbor = new byte[] { 0x83, 0x01, 0x02, 0x03 };
            string schema = "Text = tstr\nTriple = [int, int, int]";

            Assert.True(CddlTool.Validate(schema, "Triple", cbor).Ok);
            Assert.False(CddlTool.Validate(schema, "Text", cbor).Ok);
        }
    }
}
