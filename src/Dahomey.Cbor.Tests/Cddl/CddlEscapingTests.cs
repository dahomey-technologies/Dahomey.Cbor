// See CddlSchemaTests.cs for why "annotations", not plain "enable".
#nullable enable annotations
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// <c>[CborProperty("...")]</c> accepts any string at all, and the emitted key is a quoted CDDL
    /// text literal, so the two escape alphabets have to be reconciled somewhere.
    /// </summary>
    public class CddlEscaped
    {
        [CborProperty("quote\"inside")]
        public int Quoted { get; set; }

        [CborProperty("back\\slash")]
        public int Backslash { get; set; }

        [CborProperty("new\nline")]
        public int Newline { get; set; }

        /// <summary>
        /// A vertical tab is the case that separates the two alphabets: C# renders it <c>\v</c> and RFC
        /// 8610 has no such escape, so a key routed through a C# literal would emit text the gem stops
        /// on.
        /// </summary>
        [CborProperty("vertical\vtab")]
        public int Vertical { get; set; }
    }

    public abstract class CddlEscapedBase
    {
        public int Id { get; set; }
    }

    [CborDiscriminator("quote\"kind")]
    public class CddlEscapedLeaf : CddlEscapedBase
    {
    }

    public class CddlEscapedHolder
    {
        public CddlEscapedBase Item { get; set; }
    }

    [CborSerializable(typeof(CddlEscaped))]
    [CborSerializable(typeof(CddlEscapedHolder))]
    [CborSerializable(typeof(CddlEscapedLeaf))]
    [CborCddlSchema]
    public partial class CddlEscapingContext : CborSerializerContext
    {
    }

    public class CddlEscapingTests
    {
        private static readonly CddlEscapingContext Context =
            CborSerializerContext.Default<CddlEscapingContext>();

        [Fact]
        public void MemberKeysAreEscapedAsCddlTextLiterals()
        {
            string schema = CddlEscapingContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("\"quote\\\"inside\": ", schema);
            Assert.Contains("\"back\\\\slash\": ", schema);
            Assert.Contains("\"new\\nline\": ", schema);
        }

        /// <summary>
        /// <c>\v</c> is a C# escape and not an RFC 8610 one, so the vertical tab has to come out as
        /// <c>\u000b</c>. Asserting the absence of <c>\v</c> as well, because the schema would still
        /// contain a plausible-looking key without it.
        /// </summary>
        [Fact]
        public void CSharpOnlyEscapesAreNotEmitted()
        {
            string schema = CddlEscapingContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("\"vertical\\u000btab\": ", schema);
            Assert.DoesNotContain("\\v", schema);
        }

        /// <summary>
        /// The discriminator is stored twice on purpose: as a C# literal for the registration emitter to
        /// paste into generated code, and as raw text for this. A schema reusing the C# literal would
        /// emit <c>"quote"kind"</c>.
        /// </summary>
        [Fact]
        public void StringDiscriminatorsAreEscapedAsCddlTextLiterals()
        {
            Assert.Contains(
                "\"_t\": \"quote\\\"kind\",",
                CddlEscapingContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [CddlFact]
        public void TheSchemaParses()
        {
            CddlResult result = CddlTool.Parse(CddlEscapingContext.CddlSchema);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// Parsing is necessary but not sufficient: the escaped literal also has to denote the very
        /// string the serializer writes, which only a real instance check shows.
        /// </summary>
        [CddlFact]
        public void SerializerOutputWithAwkwardKeysValidates()
        {
            CddlEscaped value = new CddlEscaped { Quoted = 1, Backslash = 2, Newline = 3, Vertical = 4 };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlEscapingContext.CddlSchema, "CddlEscaped", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void SerializerOutputWithAnAwkwardDiscriminatorValidates()
        {
            CddlEscapedHolder value = new CddlEscapedHolder
            {
                Item = new CddlEscapedLeaf { Id = 7 },
            };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlEscapingContext.CddlSchema, "CddlEscapedHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
