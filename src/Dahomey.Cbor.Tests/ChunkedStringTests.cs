using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// RFC 8949 section 3.2.3 indefinite-length strings: a byte or text string encoded as zero or more
    /// definite-length chunks terminated by a break.
    /// </summary>
    /// <remarks>
    /// This is core format rather than an extension — any encoder may emit it, and it is what a producer
    /// streaming a value whose length it does not yet know will emit. An indefinite-length string
    /// denotes exactly the concatenation of its chunks, so the chunk boundaries carry no meaning and a
    /// reader cannot distinguish one from the definite-length string of the same content.
    /// <para>
    /// <c>CborDataItemScanner</c> already walked this shape, so before this the scanner would report a
    /// document <c>Complete</c> that <c>Cbor.Deserialize</c> then refused.
    /// </para>
    /// </remarks>
    public class ChunkedStringTests
    {
        /// <summary>
        /// The chunked form and the definite-length form of the same value must be indistinguishable.
        /// </summary>
        [Theory]
        // 7f 62"st" 64"ream" ff              -> "stream"
        [InlineData("7F627374647265616DFF", "stream")]
        // 7f 61"a" 61"b" 61"c" ff            -> "abc", three chunks
        [InlineData("7F616161626163FF", "abc")]
        // 7f 66"stream" ff                   -> a single chunk
        [InlineData("7F6673747265616DFF", "stream")]
        // 7f ff                              -> zero chunks, the empty string
        [InlineData("7FFF", "")]
        public void AChunkedTextStringReadsAsItsConcatenation(string hexBuffer, string expected)
        {
            Assert.Equal(expected, Helper.Read<string>(hexBuffer.Replace(" ", string.Empty)));
        }

        [Theory]
        // 5f 42 0102 43 030405 ff
        [InlineData("5F42010243030405FF", new byte[] { 1, 2, 3, 4, 5 })]
        // 5f ff -- zero chunks
        [InlineData("5FFF", new byte[0])]
        // 5f 40 42 0102 ff -- an empty chunk, which is the one shape that copies zero bytes
        [InlineData("5F40420102FF", new byte[] { 1, 2 })]
        public void AChunkedByteStringReadsAsItsConcatenation(string hexBuffer, byte[] expected)
        {
            // Helper.Read rather than Cbor.Deserialize: it runs the same bytes through the span reader,
            // a single-segment sequence and a one-byte-per-segment sequence. The last is what matters
            // here -- the scratch buffer only exists in sequence mode, so it is the only shape that
            // would catch ReadChunk asking for it and one chunk overwriting the previous.
            Assert.Equal(expected, Helper.Read<byte[]>(hexBuffer));
        }

        /// <summary>
        /// <see cref="CborReader.ReadByteStringSequence"/> is the third entry point that had to learn
        /// the encoding, and the only one that hands back a <see cref="ReadOnlySequence{T}"/>. The
        /// chunks are not contiguous in the input, so it returns the joined copy rather than a slice.
        /// </summary>
        /// <remarks>
        /// Run over a span reader and over a fragmented one. There is no converter registered for
        /// <see cref="ReadOnlySequence{T}"/>, so <c>Helper.Read</c> cannot reach this entry point and
        /// the reader has to be built directly; a sequence-backed reader is the only shape in which
        /// the scratch buffer this path declines exists at all.
        /// <para>
        /// The three-single-byte-chunk case is the one that binds the window: the accumulator grows by
        /// doubling, so three bytes arrive in a four-byte array, and handing the array back whole would
        /// append a trailing zero.
        /// </para>
        /// </remarks>
        [Theory]
        // 5f 42 0102 43 030405 ff -- two chunks, and the accumulator happens to end up exactly sized
        [InlineData("5F42010243030405FF", new byte[] { 1, 2, 3, 4, 5 })]
        // 5f 41 01 41 02 41 03 ff -- three one-byte chunks: 1 -> 2 -> 4, so the last resize overshoots
        [InlineData("5F410141024103FF", new byte[] { 1, 2, 3 })]
        // 5f ff -- zero chunks
        [InlineData("5FFF", new byte[0])]
        public void AChunkedByteStringReadsAsASequence(string hexBuffer, byte[] expected)
        {
            byte[] buffer = hexBuffer.HexToBytes();

            Assert.Equal(expected, new CborReader(buffer).ReadByteStringSequence().ToArray());
            Assert.Equal(expected, new CborReader(Helper.Fragmentize(buffer)).ReadByteStringSequence().ToArray());
        }

        /// <summary>
        /// A multi-byte character split across a chunk boundary is <em>accepted</em>, not refused.
        /// </summary>
        /// <remarks>
        /// RFC 8949 section 3.2.3 tells producers not to do this — a chunk boundary is supposed to fall
        /// on a character boundary — so accepting it is a lenient reading rather than a conformance
        /// requirement. It is deliberate, and consistent with the leniency already in the reader:
        /// <c>Encoding.UTF8.GetString</c> substitutes U+FFFD for invalid sequences rather than
        /// throwing, so refusing this particular malformation while silently repairing others would be
        /// arbitrary. <c>System.Formats.Cbor</c> in strict mode rejects it.
        /// <para>
        /// Joining the bytes before decoding is right either way: it is what makes the chunked and
        /// definite-length forms of the same value indistinguishable, which is what section 3.2.3
        /// requires of a reader whoever emitted the document.
        /// </para>
        /// </remarks>
        [Fact]
        public void AUtf8CharacterSplitAcrossChunksIsAccepted()
        {
            // 7f 61 e2      -- first byte of U+20AC EURO SIGN, alone in its chunk
            //    62 82ac    -- its remaining two bytes
            // ff
            Assert.Equal("\u20ac", Helper.Read<string>("7F61E2628 2ACFF".Replace(" ", string.Empty)));
        }

        /// <summary>The chunked form reaches an object member like any other string.</summary>
        [Fact]
        public void AChunkedStringReadsIntoAMember()
        {
            // a1 6141 7f 62"st" 64"ream" ff
            ChunkedHolder holder = Helper.Read<ChunkedHolder>("A161417F627374647265616DFF");

            Assert.Equal("stream", holder.A);
        }

        public class ChunkedHolder
        {
            [Attributes.CborProperty("A")]
            public string A { get; set; }
        }

        /// <summary>And into the object model, where it is an ordinary string afterwards.</summary>
        [Fact]
        public void AChunkedStringReadsIntoTheObjectModel()
        {
            CborValue value = Cbor.Deserialize<CborValue>("7F627374647265616DFF".HexToBytes());

            Assert.Equal(CborValueType.String, value.Type);
            Assert.Equal("stream", value.Value<string>());
        }

        /// <summary>
        /// Skipping a chunked string still walks it. An unmatched member is skipped rather than read, so
        /// without this a chunked value would derail the rest of the map.
        /// </summary>
        [Fact]
        public void AChunkedStringIsSkippedCorrectly()
        {
            // a2 6142 7f 62"st" 64"ream" ff   -- "B" is not a member of ChunkedHolder, so it is skipped
            //    6141 6161                    -- and "A" after it must still be found
            ChunkedHolder holder = Helper.Read<ChunkedHolder>(
                "A261427F627374647265616DFF61416161");

            Assert.Equal("a", holder.A);
        }

        /// <summary>
        /// A chunk of the wrong major type is malformed rather than something to coerce: a byte string
        /// chunk inside a text string would otherwise become text nobody wrote.
        /// </summary>
        [Fact]
        public void AChunkOfTheWrongMajorTypeIsRejected()
        {
            // 7f 62"st" 42 0102 ff  -- a byte string chunk inside an indefinite-length text string
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<string>("7F627374420102FF".HexToBytes()));

            Assert.Contains("indefinite-length string chunk", exception.Message);
        }

        /// <summary>Nesting is not permitted, and is refused rather than flattened.</summary>
        [Fact]
        public void ANestedIndefiniteLengthStringIsRejected()
        {
            // 7f 7f 61"a" ff ff
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<string>("7F7F6161FFFF".HexToBytes()));

            Assert.Contains("cannot contain another indefinite-length string", exception.Message);
        }

        /// <summary>
        /// Both malformed shapes fail as <see cref="CborException"/> rather than as something a caller's
        /// <c>catch (CborException)</c> would miss, which is what the refusal they replace did.
        /// </summary>
        [Theory]
        [InlineData("7F627374420102FF")]
        [InlineData("7F7F6161FFFF")]
        public void MalformedChunksThrowCborException(string hexBuffer)
        {
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<string>(hexBuffer.Replace(" ", string.Empty).HexToBytes()));
        }

        /// <summary>
        /// The chunked form is accepted everywhere the scanner already said it would be — which is the
        /// inconsistency this closes, since the scanner reported such a document complete while the
        /// reader refused it.
        /// </summary>
        [Fact]
        public void TheScannerAndTheReaderAgreeOnAChunkedString()
        {
            byte[] buffer = "7F627374647265616DFF".HexToBytes();

            Assert.True(CborDataItemScanner.TryGetDataItemLength(buffer, out int length));
            Assert.Equal(buffer.Length, length);
            Assert.Equal("stream", Cbor.Deserialize<string>(buffer));
        }

        /// <summary>
        /// A chunked string inside an indefinite-length array, so the two indefinite encodings do not
        /// interfere: the string's break must not be taken for the array's.
        /// </summary>
        [Fact]
        public void AChunkedStringInsideAnIndefiniteLengthArray()
        {
            // 9f                              array(*)
            //    7f 62"st" 64"ream" ff        "stream"
            //    61 61                        "a"
            // ff
            List<string> items = Cbor.Deserialize<List<string>>(
                "9F7F627374647265616DFF6161FF".HexToBytes());

            Assert.Equal(new[] { "stream", "a" }, items);
        }
    }
}
