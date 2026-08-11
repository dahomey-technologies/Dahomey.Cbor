// Annotations-only nullable context: the pinned schema below expects a bare tstr for Name, not
// tstr / nil. See CddlSchemaTests.cs for the fuller rationale.
#nullable enable annotations
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    [CborObjectFormat(CborObjectFormat.IntKeyMap)]
    public class CddlPacked
    {
        [CborProperty(2)]
        public string Name { get; set; }

        [CborProperty(1)]
        public int Id { get; set; }
    }

    [CborObjectFormat(CborObjectFormat.Array)]
    public class CddlRow
    {
        [CborProperty(2)]
        public string Name { get; set; }

        [CborProperty(1)]
        public int Id { get; set; }
    }

    [CborSerializable(typeof(CddlPacked))]
    [CborSerializable(typeof(CddlRow))]
    [CborCddlSchema]
    public partial class CddlFormatContext : CborSerializerContext
    {
    }

    public class CddlObjectFormatTests
    {
        private static readonly CddlFormatContext Context =
            CborSerializerContext.Default<CddlFormatContext>();

        [Fact]
        public void IntKeyMapUsesIntegerKeysSortedByIndex()
        {
            string schema = CddlFormatContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlPacked = {\n  1: -2147483648..2147483647,\n  2: tstr,\n}", schema);
        }

        /// <summary>
        /// ObjectMapping re-sorts members by ascending index at registration, so declared order is not
        /// wire order. For the Array format the schema is positional, which makes the sort a
        /// correctness requirement rather than a tidiness one: Name is declared first and written
        /// second.
        /// </summary>
        [Fact]
        public void ArrayIsPositionalInIndexOrderRatherThanDeclaredOrder()
        {
            string schema = CddlFormatContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("CddlRow = [\n  -2147483648..2147483647,\n  tstr,\n]", schema);
        }

        [CddlFact]
        public void IntKeyMapOutputValidates()
        {
            CddlPacked value = new CddlPacked { Id = 7, Name = "foo" };
            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlFormatContext.CddlSchema, "CddlPacked", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void ArrayOutputValidates()
        {
            CddlRow value = new CddlRow { Id = 7, Name = "foo" };
            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlFormatContext.CddlSchema, "CddlRow", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// The positional order has to be pinned from the wire side too: a schema of [tstr, int] would
        /// pass a shape check and fail here.
        /// </summary>
        [CddlFact]
        public void ArraySchemaRejectsTheDeclaredOrder()
        {
            // ["foo", 7] -- the declared order rather than the index order.
            byte[] cbor = "8263666F6F07".HexToBytes();

            CddlResult result = CddlTool.Validate(CddlFormatContext.CddlSchema, "CddlRow", cbor);

            Assert.False(result.Ok);
        }
    }
}
