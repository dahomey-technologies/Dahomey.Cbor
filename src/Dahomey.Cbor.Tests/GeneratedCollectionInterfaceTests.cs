using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A model that declares interfaces rather than concrete collections, which is an ordinary thing to
    /// do and was <c>CBOR1002</c> for a generated context until the interface converters were emitted.
    /// </summary>
    public class GeneratedInterfaceHolder
    {
        public IList<int> List { get; set; }

        public ICollection<string> Collection { get; set; }

        public IEnumerable<int> Enumerable { get; set; }

        public IReadOnlyList<int> ReadOnlyList { get; set; }

        public IReadOnlyCollection<int> ReadOnlyCollection { get; set; }

        public ISet<int> Set { get; set; }

        public IDictionary<string, int> Dictionary { get; set; }
    }

    [CborSerializable(typeof(GeneratedInterfaceHolder))]
    [CborCddlSchema]
    public partial class InterfaceCollectionContext : CborSerializerContext
    {
    }

    /// <summary>
    /// The interface converters resolve to the same concrete backing collections the reflection path
    /// picks, so the two paths agree on the wire and each reads what the other wrote.
    /// </summary>
    /// <remarks>
    /// <c>GeneratedCorpusTests</c> covers this type too, by assembly scan, which is where byte identity
    /// against the reflection path is actually asserted. What is here is the part a byte comparison
    /// cannot show: that the concrete type handed back is the one the interface promised, since a
    /// converter returning a <c>List&lt;int&gt;</c> where a <c>HashSet&lt;int&gt;</c> was asked for
    /// would still write identical bytes.
    /// </remarks>
    public class GeneratedCollectionInterfaceTests
    {
        [Fact]
        public void EveryCollectionInterfaceRoundTripsThroughAGeneratedContext()
        {
            InterfaceCollectionContext context = new InterfaceCollectionContext();
            GeneratedInterfaceHolder value = Sample();

            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value), generated, ignoreCase: true);

            GeneratedInterfaceHolder read =
                Cbor.Deserialize<GeneratedInterfaceHolder>(generated.HexToBytes(), context.Options);

            Assert.Equal(new[] { 1, 2 }, read.List);
            Assert.Equal(new[] { "a" }, read.Collection);
            Assert.Equal(new[] { 3 }, read.Enumerable);
            Assert.Equal(new[] { 4, 5 }, read.ReadOnlyList);
            Assert.Equal(new[] { 6 }, read.ReadOnlyCollection);
            Assert.Equal(new[] { 7 }, read.Set);
            Assert.Equal(8, read.Dictionary["k"]);
        }

        /// <summary>
        /// The backing type is chosen per interface, not one for all of them: <c>ISet&lt;T&gt;</c> must
        /// come back as something that is actually a set, and a <c>List&lt;T&gt;</c> satisfies the
        /// compiler nowhere and the bytes everywhere. This is what the corpus comparison cannot see.
        /// </summary>
        [Fact]
        public void TheBackingCollectionMatchesWhatTheInterfacePromised()
        {
            InterfaceCollectionContext context = new InterfaceCollectionContext();
            string generated = Helper.Write(Sample(), context.Options);

            GeneratedInterfaceHolder read =
                Cbor.Deserialize<GeneratedInterfaceHolder>(generated.HexToBytes(), context.Options);

            Assert.IsType<List<int>>(read.List);
            Assert.IsType<HashSet<int>>(read.Set);
            Assert.IsType<Dictionary<string, int>>(read.Dictionary);
        }

        /// <summary>
        /// A collection interface describes the same document as the concrete collection behind it, so
        /// the schema says <c>[* int]</c> and <c>{* tstr =&gt; int}</c> — the backing type decides what
        /// the reader hands back, not what goes on the wire.
        /// </summary>
        /// <remarks>
        /// <c>CddlSchemaParsesTests</c> already runs this context's schema through the reference tool by
        /// assembly scan, which says it is well-formed. This says it is *right*: a rendering that lost
        /// the element type, or emitted the backing type's name as a rule, would parse just as happily.
        /// </remarks>
        [Fact]
        public void TheSchemaDescribesTheDocumentRatherThanTheBackingType()
        {
            string schema = InterfaceCollectionContext.CddlSchema.Replace("\r\n", "\n");

            // `int` renders as its explicit range, and a member declared nullable-oblivious admits nil.
            const string Int = "-2147483648..2147483647";

            Assert.Contains($"\"List\": [* {Int}]", schema);
            Assert.Contains($"\"Set\": [* {Int}]", schema);
            Assert.Contains($"\"ReadOnlyList\": [* {Int}]", schema);
            Assert.Contains($"\"Dictionary\": {{* tstr => {Int}}}", schema);
            Assert.DoesNotContain("HashSet", schema);
            Assert.DoesNotContain("List<", schema);
        }

        internal static GeneratedInterfaceHolder Sample()
        {
            return new GeneratedInterfaceHolder
            {
                List = new List<int> { 1, 2 },
                Collection = new List<string> { "a" },
                Enumerable = new List<int> { 3 },
                ReadOnlyList = new List<int> { 4, 5 },
                ReadOnlyCollection = new List<int> { 6 },
                Set = new HashSet<int> { 7 },
                Dictionary = new Dictionary<string, int> { ["k"] = 8 },
            };
        }
    }
}
