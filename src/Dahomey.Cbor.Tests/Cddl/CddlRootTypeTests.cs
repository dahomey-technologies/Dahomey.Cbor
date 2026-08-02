// See CddlSchemaTests.cs for why "annotations", not plain "enable". A typeof() argument in an
// annotations context is NotAnnotated, which is what keeps the element references below bare.
#nullable enable annotations
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public class CddlRootItem
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// A collection, an array and a dictionary declared as roots in their own right -- the documented
    /// <c>[CborSerializable(typeof(List&lt;Person&gt;))]</c> shape, which the registration emitter gives
    /// a typed accessor. The schema owes each of them a rule: a schema describing only
    /// <see cref="CddlRootItem"/> would say nothing at all about the document the user actually writes.
    /// </summary>
    [CborSerializable(typeof(List<CddlRootItem>))]
    [CborSerializable(typeof(CddlRootItem[]))]
    [CborSerializable(typeof(Dictionary<string, CddlRootItem>))]
    [CborCddlSchema]
    public partial class CddlRootContext : CborSerializerContext
    {
    }

    public class CddlRootTypeTests
    {
        private static readonly CddlRootContext Context = CborSerializerContext.Default<CddlRootContext>();

        [Fact]
        public void ACollectionRootGetsARuleOfItsOwn()
        {
            Assert.Contains(
                "ListOfCddlRootItem = [* CddlRootItem]\n",
                CddlRootContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [Fact]
        public void AnArrayRootGetsARuleOfItsOwn()
        {
            Assert.Contains(
                "ArrayOfCddlRootItem = [* CddlRootItem]\n",
                CddlRootContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [Fact]
        public void ADictionaryRootGetsARuleOfItsOwn()
        {
            Assert.Contains(
                "DictionaryOfStringCddlRootItem = {* tstr => CddlRootItem}\n",
                CddlRootContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// A root rule carries no <c>/ nil</c>: a <c>typeof(...)</c> argument has no nullable annotation
        /// to follow, and object and enum roots have always been emitted bare, so this keeps the three
        /// collection-shaped roots consistent with them.
        /// </summary>
        [Fact]
        public void ARootRuleIsNotNilable()
        {
            Assert.DoesNotContain(
                "ListOfCddlRootItem = [* CddlRootItem] / nil",
                CddlRootContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [CddlFact]
        public void ACollectionRootValidatesAgainstItsOwnRule()
        {
            List<CddlRootItem> value = new List<CddlRootItem>
            {
                new CddlRootItem { Id = 1 },
                new CddlRootItem { Id = 2 },
            };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlRootContext.CddlSchema, "ListOfCddlRootItem", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void ADictionaryRootValidatesAgainstItsOwnRule()
        {
            Dictionary<string, CddlRootItem> value = new Dictionary<string, CddlRootItem>
            {
                ["first"] = new CddlRootItem { Id = 1 },
            };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlRootContext.CddlSchema, "DictionaryOfStringCddlRootItem", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
