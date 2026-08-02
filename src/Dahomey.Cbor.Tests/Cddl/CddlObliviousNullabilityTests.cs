// No `#nullable` directive, deliberately, and this is the point of the file. The test project leaves
// Nullable disabled, so every reference type below has NullableAnnotation.None -- the state a consumer
// project without <Nullable>enable</Nullable> is in, and the one RULING B treats as nilable. Every
// other CDDL fixture opens with `#nullable enable annotations`, which routes around this path
// entirely; that is what let an unparseable dictionary key survive ten task-scoped reviews.
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public class CddlOblivious
    {
        /// <summary>
        /// The key must render as a bare <c>tstr</c> even though the key type is exactly as nilable as
        /// the value type: RFC 8610's <c>memberkey</c> is a <c>type1</c>, so <c>tstr / nil =&gt;</c> is a
        /// parse error, and <c>Dictionary&lt;TKey,TValue&gt;</c> throws on a null key anyway.
        /// </summary>
        public Dictionary<string, string> Lookup { get; set; }

        /// <summary>Elements carry their own nilability, which here is None and so nilable.</summary>
        public List<string> Items { get; set; }
    }

    [CborSerializable(typeof(CddlOblivious))]
    [CborCddlSchema]
    public partial class CddlObliviousContext : CborSerializerContext
    {
    }

    public class CddlObliviousNullabilityTests
    {
        private static readonly CddlObliviousContext Context =
            CborSerializerContext.Default<CddlObliviousContext>();

        [Fact]
        public void DictionaryKeysAreNeverNilable()
        {
            Assert.Contains(
                "\"Lookup\": {* tstr => tstr / nil} / nil,\n",
                CddlObliviousContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [Fact]
        public void CollectionElementsCarryTheirOwnNilability()
        {
            Assert.Contains(
                "\"Items\": [* tstr / nil] / nil,\n",
                CddlObliviousContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// The regression that the schema-text assertions above cannot make on their own: text that
        /// mentions <c>tstr</c> in the right places is still worthless if the gem refuses to read it.
        /// </summary>
        [CddlFact]
        public void TheSchemaParses()
        {
            CddlResult result = CddlTool.Parse(CddlObliviousContext.CddlSchema);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// Null elements and null dictionary values really do go out as F6, so the <c>/ nil</c> on both
        /// is load-bearing rather than defensive; a null key is not tested because
        /// <c>Dictionary&lt;TKey,TValue&gt;</c> refuses to hold one.
        /// </summary>
        [CddlFact]
        public void SerializerOutputWithNullElementsAndValuesValidates()
        {
            CddlOblivious value = new CddlOblivious
            {
                Lookup = new Dictionary<string, string> { ["present"] = "x", ["absent"] = null },
                Items = new List<string> { "a", null },
            };

            string hex = Helper.Write(value, Context.Options);

            Assert.Contains("F6", hex);

            CddlResult result = CddlTool.Validate(
                CddlObliviousContext.CddlSchema, "CddlOblivious", hex.HexToBytes());

            Assert.True(result.Ok, result.Output);
        }
    }
}
