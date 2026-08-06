using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #162: reading a document into the object model and writing it back dropped every semantic
    /// tag it carried.
    /// </summary>
    /// <remarks>
    /// <c>CborValueConverter.Read</c> captures the tag into <see cref="CborValue.SemanticTag"/>, but
    /// the write path switched on <see cref="CborValue.Type"/> and never read it back, so the tag was
    /// silently lost. The tag is often what carries the meaning — tag 1 makes an integer an epoch
    /// datetime, tag 2 makes a byte string a bignum, tag 39 carries a discriminator — so dropping it
    /// changed what the document said rather than merely losing an annotation, and raised no error.
    /// <para>
    /// Tags are re-emitted unconditionally, which is what makes a read-then-write lossless. A caller
    /// that reads a tag-1 value and replaces it with a string does get a document whose tag no longer
    /// matches its content, but that is the caller describing their own value: the alternative,
    /// dropping tags whose content changed, cannot be distinguished from the round trip this fixes.
    /// </para>
    /// </remarks>
    public class Issue0162
    {
        /// <summary>The repro from the issue: a tagged epoch datetime.</summary>
        [Fact]
        public void ATaggedValueSurvivesARoundTrip()
        {
            // c1                  tag(1)
            //    1a 514b67b0      1363896240
            const string hexBuffer = "C11A514B67B0";

            CborValue value = Cbor.Deserialize<CborValue>(hexBuffer.HexToBytes());

            Assert.Equal(1UL, value.SemanticTag);
            Helper.TestWrite(value, hexBuffer);
        }

        /// <summary>
        /// A tag on a value nested inside a map, which reaches the write path through
        /// <c>WriteMapItem</c> rather than the top-level call.
        /// </summary>
        [Fact]
        public void ATagOnAMapValueSurvives()
        {
            // a1                  map(1)
            //    61 61            "a"
            //    c1 1a 514b67b0   tag(1) 1363896240
            const string hexBuffer = "A16161C11A514B67B0";

            CborObject obj = Cbor.Deserialize<CborObject>(hexBuffer.HexToBytes());

            Assert.Equal(1UL, obj["a"].SemanticTag);
            Helper.TestWrite(obj, hexBuffer);
        }

        /// <summary>A tag on a map <em>key</em>, which is written by the same call as the value.</summary>
        [Fact]
        public void ATagOnAMapKeySurvives()
        {
            // a1                  map(1)
            //    c1 01            tag(1) 1
            //    02               2
            const string hexBuffer = "A1C10102";

            CborObject obj = Cbor.Deserialize<CborObject>(hexBuffer.HexToBytes());

            Helper.TestWrite(obj, hexBuffer);
        }

        /// <summary>A tag on an array item, reached through <c>WriteArrayItem</c>.</summary>
        [Fact]
        public void ATagOnAnArrayItemSurvives()
        {
            // 82                  array(2)
            //    c1 01            tag(1) 1
            //    02               2
            const string hexBuffer = "82C10102";

            CborArray array = Cbor.Deserialize<CborArray>(hexBuffer.HexToBytes());

            Assert.Equal(1UL, array[0].SemanticTag);
            Helper.TestWrite(array, hexBuffer);
        }

        /// <summary>
        /// A tag on the container itself. The map and array writers are reachable directly — a member
        /// declared <see cref="CborObject"/> does not go through the <see cref="CborValue"/> switch —
        /// so they have to emit the tag themselves rather than rely on their caller.
        /// </summary>
        [Fact]
        public void ATagOnAMapItselfSurvives()
        {
            // c1                  tag(1)
            //    a1 6161 01       {"a": 1}
            const string hexBuffer = "C1A1616101";

            CborObject obj = Cbor.Deserialize<CborObject>(hexBuffer.HexToBytes());

            Assert.Equal(1UL, obj.SemanticTag);
            Helper.TestWrite(obj, hexBuffer);
        }

        /// <summary>The same for an array, written through the array converter directly.</summary>
        [Fact]
        public void ATagOnAnArrayItselfSurvives()
        {
            // c1                  tag(1)
            //    82 01 02         [1, 2]
            const string hexBuffer = "C1820102";

            CborArray array = Cbor.Deserialize<CborArray>(hexBuffer.HexToBytes());

            Assert.Equal(1UL, array.SemanticTag);
            Helper.TestWrite(array, hexBuffer);
        }

        /// <summary>
        /// A tag deeper than one level, so nesting is asserted rather than assumed: the tagged value
        /// sits inside an array inside a map.
        /// </summary>
        [Fact]
        public void ATagNestedTwoLevelsDeepSurvives()
        {
            // a1                  map(1)
            //    61 61            "a"
            //    81               array(1)
            //       c2 41 01      tag(2) bytes(1) 01     -- a bignum
            const string hexBuffer = "A1616181C24101";

            CborObject obj = Cbor.Deserialize<CborObject>(hexBuffer.HexToBytes());

            Assert.Equal(2UL, ((CborArray)obj["a"])[0].SemanticTag);
            Helper.TestWrite(obj, hexBuffer);
        }

        /// <summary>
        /// Every tagged shape the issue lists, so the fix is not specific to the one it repros with.
        /// </summary>
        [Theory]
        [InlineData("C07818323031332D30332D32315432303A30343A30302E353A3030")] // tag 0, RFC 3339 string
        [InlineData("C11A514B67B0")]                                          // tag 1, epoch datetime
        [InlineData("C249010000000000000000")]                                // tag 2, positive bignum
        [InlineData("C349010000000000000000")]                                // tag 3, negative bignum
        [InlineData("D8206A687474703A2F2F612E62")]                            // tag 32, URI
        [InlineData("D82763666F6F")]                                          // tag 39, discriminator
        [InlineData("D855480000C03F00002040")]                                // tag 85, float32 typed array
        public void EveryTaggedShapeSurvives(string hexBuffer)
        {
            CborValue value = Cbor.Deserialize<CborValue>(hexBuffer.HexToBytes());

            Helper.TestWrite(value, hexBuffer);
        }

        /// <summary>
        /// A tag must not escape the document it came from.
        /// </summary>
        /// <remarks>
        /// <see cref="CborPositive"/> and <see cref="CborNegative"/> hand out shared instances for
        /// small integers, as <see cref="CborSingle"/>, <see cref="CborDouble"/> and
        /// <see cref="CborDecimal"/> do for small whole numbers, <see cref="CborBoolean"/> for both
        /// values and <see cref="CborValue.Null"/> for null. Assigning <c>SemanticTag</c> to one of
        /// those attaches the tag to every occurrence of that value in the process — including
        /// documents that never carried a tag, and values built in code that were never read at all.
        /// <para>
        /// Reading captures the tag onto a copy for exactly this reason. Without that, emitting tags on
        /// write turns a latent mutation into wrong bytes: this test's second document would serialize
        /// as <c>82 C101 02</c>.
        /// </para>
        /// </remarks>
        [Fact]
        public void ATagOnOneDocumentDoesNotLeakIntoAnother()
        {
            CborValue tagged = Cbor.Deserialize<CborValue>("C101".HexToBytes());
            Assert.Equal(1UL, tagged.SemanticTag);

            // A different document, carrying no tag, whose items are the same integers.
            CborArray other = Cbor.Deserialize<CborArray>("820102".HexToBytes());
            Assert.Null(other[0].SemanticTag);
            Helper.TestWrite(other, "820102");

            // And a value built in code, which was never read from anything.
            Helper.TestWrite(new CborArray { 1, 2 }, "820102");
        }

        /// <summary>The same for the shared null, which is a single instance for the whole process.</summary>
        [Fact]
        public void ATagOnANullDoesNotLeakIntoTheSharedNull()
        {
            CborValue tagged = Cbor.Deserialize<CborValue>("C2F6".HexToBytes());

            Assert.Equal(2UL, tagged.SemanticTag);
            Assert.Null(CborValue.Null.SemanticTag);
            Helper.TestWrite<CborValue>(CborValue.Null, "F6");
        }

        /// <summary>
        /// An untagged document is byte-identical to what it was before, which is the guarantee that
        /// matters to every caller who never meets a tag.
        /// </summary>
        [Fact]
        public void AnUntaggedDocumentIsUnchanged()
        {
            // a2 6161 01 6162 82 02 03  -- {"a": 1, "b": [2, 3]}
            const string hexBuffer = "A26161016162820203";

            CborObject obj = Cbor.Deserialize<CborObject>(hexBuffer.HexToBytes());

            Assert.Null(obj.SemanticTag);
            Helper.TestWrite(obj, hexBuffer);
        }
    }
}
