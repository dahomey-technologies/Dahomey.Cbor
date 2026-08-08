using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Dahomey.Cbor.Util;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #135: a read failure reported the offending byte but not where in the model it was
    /// reached, leaving the caller to map an offset back onto a POCO graph by hand.
    /// </summary>
    /// <remarks>
    /// The path is assembled as the exception unwinds rather than tracked as the reader descends: the
    /// reader is a <c>ref struct</c> that knows major types, not members, and the hot structural reads
    /// are deliberately inlineable. Each converter names its own position on the way out, so
    /// well-formed data pays nothing.
    /// <para>
    /// A path of <c>$</c> means the root value itself was wrong; a null <see cref="CborException.Path"/>
    /// means no converter on the way up could name a position at all, which is what a failure inside a
    /// caller-supplied converter looks like. Those are different answers and are kept apart.
    /// </para>
    /// </remarks>
    public class Issue0135
    {
        private class Child
        {
            public string Name { get; set; }
        }

        private class Root
        {
            public Child Child { get; set; }
            public List<Child> Items { get; set; }
            public Dictionary<string, int> Map { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyed
        {
            [CborProperty(1)]
            public int Value { get; set; }
        }

        /// <summary>
        /// The failure the issue opened with: the document contradicts the requested type outright.
        /// There is no member to name, but "at the root" is itself the answer.
        /// </summary>
        [Fact]
        public void ARootLevelFailureIsReportedAsTheRoot()
        {
            // 01   1, where a map was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("01".HexToBytes()));

            Assert.Equal("$", exception.Path);
            Assert.Contains("Failed to deserialize from \"$\".", exception.Message);
        }

        [Fact]
        public void AMemberIsNamedByItsPath()
        {
            // a1                  map(1)
            //    64 4e616d65      "Name"
            //    01               1, where a text string was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Name", exception.Path);
        }

        /// <summary>The byte offset the reader already reported is kept, not replaced.</summary>
        [Fact]
        public void ThePathIsAddedToTheExistingMessageRatherThanReplacingIt()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A1644E616D6501".HexToBytes()));

            Assert.StartsWith("[", exception.Message);
            Assert.EndsWith(" Failed to deserialize from \"$.Name\".", exception.Message);
        }

        [Fact]
        public void NestedObjectsCompose()
        {
            // a1                  map(1)
            //    65 4368696c64    "Child"
            //    a1               map(1)
            //       64 4e616d65   "Name"
            //       01            1
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654368696C64A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Child.Name", exception.Path);
        }

        /// <summary>
        /// A member whose value is the wrong shape entirely fails before any of its own members are
        /// reached, so the innermost thing that can be named is the member itself.
        /// </summary>
        [Fact]
        public void AMemberOfTheWrongShapeIsNamedWithoutAnInnerSegment()
        {
            // a1                  map(1)
            //    65 4368696c64    "Child"
            //    01               1, where a map was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654368696C6401".HexToBytes()));

            Assert.Equal("$.Child", exception.Path);
        }

        [Fact]
        public void CollectionItemsAreNamedByIndex()
        {
            // a1                     map(1)
            //    65 4974656d73       "Items"
            //    82                  array(2)
            //       a1 644e616d65 63 616263    {"Name": "abc"}
            //       a1 644e616d65 01           {"Name": 1}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654974656D7382A1644E616D6563616263A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Items[1].Name", exception.Path);
        }

        /// <summary>
        /// The index is the failing item's own, not the count of items read so far, and a root
        /// collection needs no member to hang off.
        /// </summary>
        [Fact]
        public void ARootCollectionNamesTheFailingIndex()
        {
            // 82            array(2)
            //    01         1
            //    63 616263  "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<List<int>>("820163616263".HexToBytes()));

            Assert.Equal("$[1]", exception.Path);
        }

        /// <summary>A failure on the array header is not attributed to its first item.</summary>
        [Fact]
        public void ACollectionOfTheWrongShapeIsNotAttributedToItemZero()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<List<int>>("01".HexToBytes()));

            Assert.Equal("$", exception.Path);
        }

        [Fact]
        public void DictionaryEntriesAreNamedByKey()
        {
            // a1                  map(1)
            //    63 4d6170        "Map"
            //    a2               map(2)
            //       61 61 01      "a": 1
            //       61 62 63616263  "b": "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1634D6170A2616101616263616263".HexToBytes()));

            Assert.Equal("$.Map['b']", exception.Path);
        }

        [Fact]
        public void TupleItemsAreNamedByIndex()
        {
            // 82      array(2)
            //    01   1
            //    02   2, where a text string was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<(int, string)>("820102".HexToBytes()));

            Assert.Equal("$[1]", exception.Path);
        }

        /// <summary>
        /// An integer-keyed member is worth naming by the name it has on the type: the index is what
        /// the document said, but the name is what the caller is looking for.
        /// </summary>
        [Fact]
        public void AnIntegerKeyedMemberIsNamedByItsMemberName()
        {
            // a1      map(1)
            //    01   1
            //    63 616263  "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IntKeyed>("A10163616263".HexToBytes()));

            Assert.Equal("$.Value", exception.Path);
        }

        /// <summary>
        /// A member name comes from the document, so a hostile one is quoted on the same terms as any
        /// other document text rather than copied into the path whole.
        /// </summary>
        [Fact]
        public void ALongMemberNameIsTruncatedInThePath()
        {
            string name = new string('n', 500);
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(1);
            cborWriter.WriteString(name);
            cborWriter.WriteInt32(1);

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(writer.WrittenSpan.ToArray(), options));

            Assert.DoesNotContain(name, exception.Message);
            Assert.True(exception.Message.Length < 300, $"message was {exception.Message.Length} chars");
        }

        /// <summary>
        /// Nothing is appended when a read succeeds, and the property stays null for an exception that
        /// never passed through a converter.
        /// </summary>
        [Fact]
        public void AnExceptionRaisedOutsideAnyConverterHasNoPath()
        {
            CborException exception = new CborException("boom");

            Assert.Null(exception.Path);
            Assert.Equal("boom", exception.Message);
        }
    }
}
