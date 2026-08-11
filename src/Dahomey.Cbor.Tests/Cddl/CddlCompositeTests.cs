// Annotations-only nullable context: Roslyn records NotAnnotated/Annotated on every reference-type
// member below without turning on the warning context (plain "enable" would raise CS8618 on the
// non-nullable reference members that are never initialised in this fixture). The pinned schema in
// CddlCompositeTests depends on this -- see CddlSchemaTests.cs for the fuller rationale.
#nullable enable annotations
using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public enum CddlColour
    {
        Red = 0,
        Green = 1,
    }

    public class CddlComposite
    {
        public CddlColour Colour { get; set; }
        public int? Optional { get; set; }
        public string Nullable { get; set; }
        public List<string> Tags { get; set; }
        public int[] Sizes { get; set; }
        public Dictionary<string, int> Counts { get; set; }
        public byte[] Payload { get; set; }
        public DateTime Stamp { get; set; }
    }

    [CborSerializable(typeof(CddlComposite))]
    [CborCddlSchema]
    public partial class CddlCompositeContext : CborSerializerContext
    {
    }

    public class CddlCompositeTests
    {
        private static readonly CddlCompositeContext Context =
            CborSerializerContext.Default<CddlCompositeContext>();

        private static CddlComposite Sample() => new CddlComposite
        {
            Colour = CddlColour.Green,
            Optional = 5,
            Nullable = "here",
            Tags = new List<string> { "a", "b" },
            Sizes = new[] { 1, 2 },
            Counts = new Dictionary<string, int> { ["x"] = 1 },
            Payload = new byte[] { 1, 2, 3 },
            Stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        [Fact]
        public void CompositeMembersAreEmitted()
        {
            string schema = CddlCompositeContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("\"Colour\": CddlColour,", schema);
            Assert.Contains("\"Optional\": -2147483648..2147483647 / nil,", schema);
            Assert.Contains("\"Nullable\": tstr,", schema);
            Assert.Contains("\"Tags\": [* tstr],", schema);
            Assert.Contains("\"Sizes\": [* -2147483648..2147483647],", schema);
            Assert.Contains("\"Counts\": {* tstr => -2147483648..2147483647},", schema);
            Assert.Contains("\"Payload\": bstr,", schema);
            Assert.Contains("\"Stamp\": #6.0(tstr),", schema);
        }

        [Fact]
        public void EnumIsARuleOverItsDeclaredValues()
        {
            Assert.Contains("CddlColour = 0..1", CddlCompositeContext.CddlSchema);
        }

        [CddlFact]
        public void SerializerOutputValidatesAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(Sample(), Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlCompositeContext.CddlSchema, "CddlComposite", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void SchemaRejectsAByteStringWhereAnArrayIsRequired()
        {
            // Tags replaced by a byte string; everything else as written by the serializer.
            string hexBuffer = Helper.Write(Sample(), Context.Options)
                .Replace("826161 6162".Replace(" ", string.Empty), "43010203");

            CddlResult result = CddlTool.Validate(
                CddlCompositeContext.CddlSchema, "CddlComposite", hexBuffer.HexToBytes());

            Assert.False(result.Ok);
        }
    }
}
