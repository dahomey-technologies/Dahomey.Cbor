using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// RULING B at the use sites a member cannot reach on its own: collection elements, dictionary
    /// values and dictionary keys, each rendered from the annotation on the symbol at the use site
    /// rather than from the shared <c>TypeModel</c>.
    /// </summary>
    /// <remarks>
    /// Compiled in memory rather than declared as ordinary fixtures because a member typed
    /// <c>List&lt;string?&gt;</c> makes the registration emitter raise CS8619 in the consuming project
    /// -- it names mapping types by a display format that drops the nullable-reference modifier -- and
    /// a <c>Dictionary&lt;int?, ...&gt;</c> member raises CS8714 against
    /// <c>Dictionary&lt;TKey,TValue&gt;</c>'s <c>notnull</c> constraint. Both are pre-existing and
    /// neither is this branch's to fix; the harness keeps them out of its build.
    /// </remarks>
    public class CddlNestedNullabilityTests
    {
        private const string Source = @"
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;

namespace Harness
{
    public class Nested
    {
        public List<string> RequiredItems { get; set; }
        public List<string?> OptionalItems { get; set; }
        public Dictionary<string, string> RequiredValues { get; set; }
        public Dictionary<string, string?> OptionalValues { get; set; }
        public Dictionary<string?, string> ObliviousKeys { get; set; }
        public Dictionary<int?, string> OptionalValueTypeKeys { get; set; }
    }

    [CborSerializable(typeof(Nested))]
    [CborCddlSchema]
    public partial class HarnessContext : CborSerializerContext { }
}
";

        /// <summary>
        /// The two <c>List</c> members share one <c>TypeModel</c>: <c>TypeCollector</c> keys its table
        /// on a display string that drops the nullable-reference modifier, deliberately, so that the
        /// registration emitter emits one <c>RegisterConverter</c> call for the one runtime type.
        /// Reading element nilability off that shared model therefore lets whichever member the
        /// collector reached first decide it for both, and one of the two comes out wrong whichever
        /// order that is -- which is why both are asserted rather than just the annotated one.
        /// </summary>
        [Fact]
        public void CollectionElementsFollowTheUseSiteAnnotation()
        {
            string schema = CddlGeneratorHarness.RunAndGetCddlSchema(Source);

            Assert.Contains("\"RequiredItems\": [* tstr],\n", schema);
            Assert.Contains("\"OptionalItems\": [* tstr / nil],\n", schema);
        }

        [Fact]
        public void DictionaryValuesFollowTheUseSiteAnnotation()
        {
            string schema = CddlGeneratorHarness.RunAndGetCddlSchema(Source);

            Assert.Contains("\"RequiredValues\": {* tstr => tstr},\n", schema);
            Assert.Contains("\"OptionalValues\": {* tstr => tstr / nil},\n", schema);
        }

        /// <summary>
        /// An explicitly nullable key is still not nilable in CDDL: RFC 8610's <c>memberkey</c> admits
        /// only a <c>type1</c>, so the choice would not parse, and the dictionary would throw on the
        /// null it describes.
        /// </summary>
        [Fact]
        public void DictionaryKeysAreNeverNilable()
        {
            string schema = CddlGeneratorHarness.RunAndGetCddlSchema(Source);

            Assert.Contains("\"ObliviousKeys\": {* tstr => tstr},\n", schema);
            Assert.Contains("\"OptionalValueTypeKeys\": {* -2147483648..2147483647 => tstr},\n", schema);
        }

        /// <summary>
        /// The point of the three assertions above, made where it counts: the gem reads the whole
        /// schema. A key that kept its <c>/ nil</c> would make this a parse error rather than a wrong
        /// answer, and every instance check in this folder would then be checking nothing.
        /// </summary>
        [CddlFact]
        public void TheSchemaParses()
        {
            CddlResult result = CddlTool.Parse(CddlGeneratorHarness.RunAndGetCddlSchema(Source));

            Assert.True(result.Ok, result.Output);
        }
    }
}
