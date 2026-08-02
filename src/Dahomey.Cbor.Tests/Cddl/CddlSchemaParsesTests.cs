using Dahomey.Cbor.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// The one gate every other CDDL test presupposes and none of them checks: that the emitted schema
    /// is text the reference implementation will actually read.
    /// </summary>
    /// <remarks>
    /// Every other gem call in this folder validates an <em>instance</em> against a schema, which
    /// silently assumes the schema parsed -- and an emitter can produce text that does not, in ways no
    /// amount of <c>Assert.Contains</c> against the schema string will show. RFC 8610's
    /// <c>memberkey</c> production accepting only a <c>type1</c>, so that a nilable dictionary key is a
    /// parse error, is one such way; an unescaped quote inside a member name is another. Both emit text
    /// that reads correctly and parses not at all, and a schema that does not parse cannot fail an
    /// instance check, so nothing downstream notices.
    /// <para>
    /// Every context in the assembly, not a sample: the failure this catches is one nobody predicted,
    /// so the net has to be the whole surface rather than the cases someone thought to list. Reflection
    /// rather than a hand-written table for the same reason -- a context added later is covered without
    /// anyone remembering to add it, including one that has no tests of its own.
    /// </para>
    /// </remarks>
    public class CddlSchemaParsesTests
    {
        /// <summary>
        /// Guards the enumeration itself: a reflection query that silently matched nothing would make
        /// this whole file pass vacuously, which is the same class of failure it exists to catch. The
        /// bound is a floor rather than an exact count, so adding a context does not fail it.
        /// </summary>
        private const int KnownContextCount = 20;

        [CddlFact]
        public void EveryEmittedSchemaParses()
        {
            List<(string Name, string Schema)> schemas = Schemas().ToList();

            Assert.True(
                schemas.Count >= KnownContextCount,
                $"expected at least {KnownContextCount} generated schemas, found {schemas.Count}");

            List<string> failures = new List<string>();

            foreach ((string name, string schema) in schemas)
            {
                CddlResult result = CddlTool.Parse(schema);

                if (!result.Ok)
                {
                    failures.Add(name + Environment.NewLine + result.Output);
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, failures));
        }

        /// <summary>
        /// The gate is only worth having if it can fail, and this is the exact construct that motivated
        /// it: a nilable dictionary key, which reads as perfectly sensible CDDL and is a parse error.
        /// </summary>
        [CddlFact]
        public void TheParseGateRejectsANilableDictionaryKey()
        {
            CddlResult result = CddlTool.Parse("M = { \"Counts\": {* tstr / nil => int} }\n");

            Assert.False(result.Ok);
        }

        /// <summary>
        /// The other half of the same point: an unescaped quote inside a member name closes the literal
        /// early, and the gem stops on it.
        /// </summary>
        [CddlFact]
        public void TheParseGateRejectsAnUnescapedQuoteInAMemberName()
        {
            CddlResult result = CddlTool.Parse("M = { \"a\"b\": int }\n");

            Assert.False(result.Ok);
        }

        /// <summary>
        /// Each generated context's <c>CddlSchema</c> constant, by context name. A <c>const</c> field,
        /// so it is read through <see cref="FieldInfo.GetRawConstantValue"/> rather than
        /// <c>GetValue</c>.
        /// </summary>
        private static IEnumerable<(string Name, string Schema)> Schemas()
        {
            foreach (Type type in typeof(CddlSchemaParsesTests).Assembly.GetTypes())
            {
                if (!typeof(CborSerializerContext).IsAssignableFrom(type))
                {
                    continue;
                }

                FieldInfo field = type.GetField(
                    "CddlSchema", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

                if (field is { IsLiteral: true } && field.FieldType == typeof(string))
                {
                    yield return (type.Name, (string)field.GetRawConstantValue());
                }
            }
        }
    }
}
