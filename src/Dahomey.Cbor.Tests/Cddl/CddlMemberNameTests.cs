// Annotations-only nullable context: the pinned schemas below expect a bare tstr, not tstr / nil.
// See CddlSchemaTests.cs for the fuller rationale.
#nullable enable annotations
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    [CborObjectFormat(CborObjectFormat.Array)]
    public class CddlLabelledRow
    {
        [CborProperty(0)]
        public int Id { get; set; }

        [CborProperty(1)]
        public string Name { get; set; }

        /// <summary>Second member of the same type as Id, which is what an unlabelled rule cannot tell apart.</summary>
        [CborProperty(2)]
        public int Revision { get; set; }
    }

    /// <summary>
    /// Two members whose names fold to the same ASCII identifier: `Café` escapes its non-ASCII
    /// character as its code point, which is exactly what the other member is already called.
    /// </summary>
    [CborObjectFormat(CborObjectFormat.Array)]
    public class CddlColliding
    {
        [CborProperty(0)]
        public int Café { get; set; }

        [CborProperty(1)]
        public int Caf_00E9 { get; set; }
    }

    [CborObjectFormat(CborObjectFormat.IntKeyMap)]
    public class CddlLabelledPacked
    {
        [CborProperty(1)]
        public int Id { get; set; }

        [CborProperty(2)]
        public string Name { get; set; }
    }

    [CborSerializable(typeof(CddlLabelledRow))]
    [CborSerializable(typeof(CddlColliding))]
    [CborSerializable(typeof(CddlLabelledPacked))]
    [CborCddlSchema(MemberNames = true)]
    public partial class CddlMemberNameContext : CborSerializerContext
    {
    }

    public class CddlMemberNameTests
    {
        private static readonly CddlMemberNameContext Context =
            CborSerializerContext.Default<CddlMemberNameContext>();

        private static string Schema =>
            CddlMemberNameContext.CddlSchema.Replace("\r\n", "\n");

        [Fact]
        public void ArrayEntriesCarryTheirMemberNames()
        {
            Assert.Contains(
                "CddlLabelledRow = [\n" +
                "  Id: -2147483648..2147483647,\n" +
                "  Name: tstr,\n" +
                "  Revision: -2147483648..2147483647,\n" +
                "]",
                Schema);
        }

        /// <summary>
        /// The reason the option exists: without labels the rule is three bare types, two of them
        /// identical, and a generator naming a field per entry has nothing to tell positions 0 and 2
        /// apart by.
        /// </summary>
        [Fact]
        public void WithoutTheOptionTheEntriesAreBare()
        {
            string unlabelled = CddlFormatContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlRow = [\n  -2147483648..2147483647,\n  tstr,\n]", unlabelled);
        }

        [Fact]
        public void LabelsThatFoldTogetherAreMadeUnique()
        {
            Assert.Contains(
                "CddlColliding = [\n" +
                "  Caf_00E9: -2147483648..2147483647,\n" +
                "  Caf_00E92: -2147483648..2147483647,\n" +
                "]",
                Schema);
        }

        /// <summary>
        /// The option is about the Array format alone. A map already names every entry by its key, and
        /// a label there would be a second, contradictory name for the same position.
        /// </summary>
        [Fact]
        public void MapFormatsAreUnaffected()
        {
            Assert.Contains(
                "CddlLabelledPacked = {\n  1: -2147483648..2147483647,\n  2: tstr,\n}",
                Schema);
        }

        /// <summary>
        /// The labels are documentation: an array's member keys do not participate in validation, so
        /// the bytes that validated against the unlabelled rule still validate against this one.
        /// </summary>
        [CddlFact]
        public void LabelledOutputStillValidates()
        {
            CddlLabelledRow value = new CddlLabelledRow { Id = 7, Name = "foo", Revision = 3 };
            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(Schema, "CddlLabelledRow", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// Labelling must not have turned the rule into something that accepts any order: the entries
        /// are still positional, and a document in declared-name order rather than index order is
        /// still wrong.
        /// </summary>
        [CddlFact]
        public void LabellingDoesNotMakeTheRuleOrderInsensitive()
        {
            // ["foo", 7, 3] -- Name first, where the rule says Id first.
            byte[] cbor = "8363666F6F0703".HexToBytes();

            CddlResult result = CddlTool.Validate(Schema, "CddlLabelledRow", cbor);

            Assert.False(result.Ok, result.Output);
        }
    }
}
