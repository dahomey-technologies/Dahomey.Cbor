using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #183: a data item behind more than one semantic tag was decoded from the wrong bytes.
    /// No exception — the caller got a plausible wrong number.
    /// </summary>
    /// <remarks>
    /// Two arms both assumed a tag comes alone. <c>GetCurrentDataItemType</c> stepped over the second
    /// tag with <c>Advance(1)</c> after <c>GetHeader()</c> had already taken that tag's head, so it
    /// swallowed the head of the tagged item and decoded the next byte as the value; and
    /// <c>SkipSemanticTag</c> skipped one tag only, leaving the second standing where every caller
    /// expected the item's own header — <c>SkipDataItem</c> read it as "already skipped" and returned
    /// with the item still in the buffer, desynchronising the rest of the document.
    /// <para>
    /// The depth matters, and not monotonically: the faulty arm consumed two bytes where it should
    /// have consumed one, so an even number of stacked tags resynchronised by accident and an odd
    /// number landed mid-item. The theories below therefore run every depth from 0 to 4 rather than
    /// one "nested" case, and they run it on both routes — straight to a converter, and through
    /// <see cref="CborValue"/>, which consumes one tag itself before dispatching and so enters at the
    /// other parity.
    /// </para>
    /// <para>
    /// What is still lost above one tag is the tag itself: <see cref="CborValue.SemanticTag"/> holds
    /// one, and keeps the outermost. That is the intended shortfall — RFC 8949 §3.4 permits the
    /// nesting, and a decoder that drops a tag it has nowhere to put is a different thing from one
    /// that returns the wrong value.
    /// </para>
    /// </remarks>
    public class Issue0183
    {
        // 19 012c -- 300, two bytes of argument behind the header, so a one-byte slip is visible in
        // the value rather than hidden in a single-byte item.
        private const string Value300 = "19012C";

        // c1 -- tag(1), epoch-based date-time
        private const string Tag1 = "C1";

        // d8 64 -- tag(100), a tag whose head is two bytes: Advance(1) could never have stepped over
        // this one whole, however many of them there are.
        private const string Tag100 = "D864";

        private static readonly DateTime s_epochPlus300Seconds =
            new DateTime(1970, 1, 1, 0, 5, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("")]
        [InlineData(Tag1)]
        [InlineData(Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1 + Tag1)]
        public void AStackOfTagsDoesNotShiftTheValueReadIntoCborValue(string tags)
        {
            CborValue value = Helper.Read<CborValue>(tags + Value300);

            Assert.Equal(300, value.Value<int>());
        }

        [Theory]
        [InlineData("")]
        [InlineData(Tag1)]
        [InlineData(Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1 + Tag1)]
        public void AStackOfTagsDoesNotShiftTheValueReadIntoAnInteger(string tags)
        {
            // The integer converters never reach GetCurrentDataItemType: they call SkipSemanticTag and
            // then expect an integer major type. Before the fix the second tag was that major type,
            // so this route failed loudly where the two above failed silently. Both are wrong.
            Assert.Equal(300, Helper.Read<int>(tags + Value300));
        }

        [Theory]
        [InlineData("")]
        [InlineData(Tag1)]
        [InlineData(Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1 + Tag1)]
        public void AStackOfTagsDoesNotShiftADateTime(string tags)
        {
            Assert.Equal(s_epochPlus300Seconds, Helper.Read<DateTime>(tags + Value300));
        }

        /// <summary>
        /// The original sighting, and the same defect wearing different clothes: the string's <c>74</c>
        /// header was eaten and its first character — ASCII <c>'2'</c>, <c>0x32</c>, a CBOR negative
        /// integer of −19 — was decoded as the value, giving epoch − 19s.
        /// </summary>
        [Fact]
        public void AStackOfTagsDoesNotShiftAnRfc3339DateTime()
        {
            // c0 c0                                    tag(0) tag(0)
            //    74 "2020-01-02T03:04:05Z"             RFC 3339 date-time
            DateTime dateTime = Helper.Read<DateTime>("C0C074323032302D30312D30325430333A30343A30355A");

            Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc), dateTime);
        }

        [Theory]
        [InlineData(Tag100)]
        [InlineData(Tag100 + Tag100)]
        [InlineData(Tag100 + Tag100 + Tag100)]
        [InlineData(Tag1 + Tag100)]
        [InlineData(Tag100 + Tag1)]
        public void ATagWithAMultiByteHeadIsSteppedOverWhole(string tags)
        {
            Assert.Equal(300, Helper.Read<int>(tags + Value300));
            Assert.Equal(300, Helper.Read<CborValue>(tags + Value300).Value<int>());
        }

        [Theory]
        [InlineData(Tag1, 1ul)]
        [InlineData(Tag1 + Tag1, 1ul)]
        [InlineData(Tag1 + Tag1 + Tag1, 1ul)]
        [InlineData(Tag100 + Tag1, 100ul)]
        public void TheOutermostTagOfAStackIsTheOneCborValueKeeps(string tags, ulong expectedTag)
        {
            CborValue value = Helper.Read<CborValue>(tags + Value300);

            Assert.Equal(expectedTag, value.SemanticTag);
        }

        /// <summary>
        /// What the two arms disagreed about, stated directly: after reporting the type, the reader is
        /// still standing on the item's own header, whatever the tags in front of it were.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(Tag1)]
        [InlineData(Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1)]
        [InlineData(Tag100 + Tag1)]
        public void GetCurrentDataItemTypeLeavesTheItemsOwnHeaderInPlace(string tags)
        {
            OnEveryReaderShape(tags + Value300, (ref CborReader reader) =>
            {
                Assert.Equal(CborDataItemType.Unsigned, reader.GetCurrentDataItemType());
                Assert.Equal(300ul, reader.ReadUInt64());
                Assert.False(reader.DataAvailable);
            });
        }

        /// <summary>
        /// The skip side of the same assumption: a stacked tag used to leave its item in the buffer,
        /// so whatever came next was read from the middle of the item that was supposed to be gone.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(Tag1)]
        [InlineData(Tag1 + Tag1)]
        [InlineData(Tag1 + Tag1 + Tag1)]
        [InlineData(Tag100 + Tag1)]
        public void SkipDataItemTakesTheTagsAndTheItemTheyTag(string tags)
        {
            // ... 05  -- the item after the tagged one, which a short skip would not reach
            OnEveryReaderShape(tags + Value300 + "05", (ref CborReader reader) =>
            {
                reader.SkipDataItem();

                Assert.Equal(5, reader.ReadInt32());
                Assert.False(reader.DataAvailable);
            });
        }

        public class Holder
        {
            public int B { get; set; }
        }

        /// <summary>
        /// The skip reached from the object model: an unmapped member is skipped with
        /// <c>SkipDataItem</c>, so a stacked tag on its value used to eat the member after it.
        /// </summary>
        [Fact]
        public void AStackedTagOnAnUnmappedMemberDoesNotSwallowTheFollowingPair()
        {
            // a2                     map(2)
            //    61 41               "A"
            //    c1 c1 19 012c       tag(1) tag(1) 300
            //    61 42               "B"
            //    01                  1
            Holder holder = Cbor.Deserialize<Holder>("A26141C1C119012C614201".HexToBytes());

            Assert.Equal(1, holder.B);
        }

        /// <summary>
        /// The depth of the stack is not a limit on correctness, and reading it does not grow the call
        /// stack with it.
        /// </summary>
        /// <remarks>
        /// <c>GetCurrentDataItemType</c> recursed once per tag, and stepping over the stack in a loop
        /// is what removes that. The frames were the risk rather than the observed failure: at this
        /// depth the old code returned a wrong value (1, not 300) rather than overflowing, so what this
        /// pins is the value, with the loop as the reason it holds at any length. A tag is not nesting,
        /// so <see cref="CborOptions.MaxDepth"/> never bounded this — only the loop does.
        /// </remarks>
        [Fact]
        public void ALongStackOfTagsIsReadWithoutRecursion()
        {
            const int tagCount = 200_000;

            byte[] buffer = new byte[tagCount + 3];
            buffer.AsSpan(0, tagCount).Fill(0xC1);
            Value300.HexToBytes().CopyTo(buffer.AsSpan(tagCount));

            CborReader reader = new CborReader(buffer.AsSpan());

            Assert.Equal(CborDataItemType.Unsigned, reader.GetCurrentDataItemType());
            Assert.Equal(300ul, reader.ReadUInt64());
            Assert.False(reader.DataAvailable);
        }

        private delegate void ReaderProbe(ref CborReader reader);

        /// <summary>
        /// Runs a probe over the three shapes a <see cref="CborReader"/> can be built on. The
        /// single-segment sequence and the byte-per-segment one take different paths through
        /// <c>GetBytes</c>, and this defect was about how many bytes were taken.
        /// </summary>
        private static void OnEveryReaderShape(string hexBuffer, ReaderProbe probe)
        {
            byte[] buffer = hexBuffer.HexToBytes();

            CborReader spanReader = new CborReader(buffer.AsSpan());
            probe(ref spanReader);

            CborReader sequenceReader = new CborReader(new ReadOnlySequence<byte>(buffer));
            probe(ref sequenceReader);

            CborReader fragmentedReader = new CborReader(Helper.Fragmentize(buffer));
            probe(ref fragmentedReader);
        }
    }
}
