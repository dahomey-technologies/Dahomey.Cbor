using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A document read from a stream is exactly the bytes the stream held, and nothing else. The single
    /// item overloads read into a buffer rented from a pool, and a rented buffer is longer than what was
    /// read into it, so deserializing from the whole buffer would complete a truncated document with
    /// whatever the buffer held before - zeroes at first, and later the bytes of an earlier document.
    /// </summary>
    /// <remarks>
    /// Every kind of stream below takes a different route to its buffer: a <see cref="MemoryStream"/>
    /// that exposes its buffer is read in place and never rents one, any other seekable stream rents
    /// one the size of <see cref="Stream.Length"/>, and a stream that cannot seek is read in chunks into
    /// a buffer that doubles. All of them are tested for each of the two single item overloads.
    /// <para>
    /// The reads here complete synchronously, so the buffer a read returns to the pool is the one the
    /// next read rents; that is what lets a document be primed with recognisable bytes.
    /// </para>
    /// </remarks>
    public class DeserializeAsyncStreamTests
    {
        public class RecordWithNumber
        {
            public int A { get; set; }
        }

        public class RecordWithText
        {
            public string S { get; set; }
        }

        private const string Generic = "DeserializeAsync<T>";
        private const string NonGeneric = "DeserializeAsync(Type)";

        // A1 61 53 66 736563726574 00 00 00 00 00 00      {"S": "secret"} then six bytes of padding
        private const string PrimingDocument = "A1615366736563726574000000000000";

        /// <summary>A stream with the data of a document, in whatever shape the test asks for.</summary>
        private sealed class DocumentStream : Stream
        {
            private readonly byte[] _data;
            private readonly bool _canSeek;
            private readonly int _chunk;
            private int _position;

            public DocumentStream(byte[] data, bool canSeek, int chunk)
            {
                _data = data;
                _canSeek = canSeek;
                _chunk = chunk;
            }

            public override bool CanRead => true;
            public override bool CanSeek => _canSeek;
            public override bool CanWrite => false;

            public override long Length => _canSeek ? _data.Length : throw new NotSupportedException();

            public override long Position
            {
                get => _canSeek ? _position : throw new NotSupportedException();
                set => Seek(value, SeekOrigin.Begin);
            }

            public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

            public override int Read(Span<byte> buffer)
            {
                int count = Math.Min(Math.Min(buffer.Length, _chunk), _data.Length - _position);
                _data.AsSpan(_position, count).CopyTo(buffer);
                _position += count;
                return count;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => new ValueTask<int>(Read(buffer.Span));

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => Task.FromResult(Read(buffer.AsSpan(offset, count)));

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (!_canSeek)
                {
                    throw new NotSupportedException();
                }

                _position = (int)(origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? _position + offset : _data.Length + offset);
                return _position;
            }

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static readonly Dictionary<string, Func<byte[], Stream>> Kinds = new Dictionary<string, Func<byte[], Stream>>
        {
            // Read in place, no buffer is rented: the control that has always been right.
            ["MemoryStream, buffer exposed"] = bytes =>
            {
                MemoryStream stream = new MemoryStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;
                return stream;
            },

            // The constructor taking a byte[] does not expose it.
            ["MemoryStream, buffer not exposed"] = bytes => new MemoryStream(bytes),

            ["FileStream"] = bytes =>
            {
                FileStream stream = new FileStream(
                    Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;
                return stream;
            },

            ["seekable stream"] = bytes => new DocumentStream(bytes, canSeek: true, chunk: int.MaxValue),
            ["seekable stream, one byte per read"] = bytes => new DocumentStream(bytes, canSeek: true, chunk: 1),
            ["non-seekable stream"] = bytes => new DocumentStream(bytes, canSeek: false, chunk: int.MaxValue),
            ["non-seekable stream, one byte per read"] = bytes => new DocumentStream(bytes, canSeek: false, chunk: 1),

            // Length is the whole stream, not what is left of it.
            ["seekable stream, positioned after a prefix"] = bytes =>
            {
                byte[] all = new byte[bytes.Length + 5];
                bytes.CopyTo(all, 5);
                return new MemoryStream(all) { Position = 5 };
            },
        };

        public static IEnumerable<object[]> EveryStreamAndEntryPoint()
        {
            foreach (string kind in Kinds.Keys)
            {
                yield return new object[] { kind, Generic };
                yield return new object[] { kind, NonGeneric };
            }
        }

        private static async Task<T> ReadAsync<T>(string kind, string entryPoint, byte[] bytes)
        {
            CborOptions options = new CborOptions();
            CancellationToken token = new CancellationTokenSource().Token;

            using (Stream stream = Kinds[kind](bytes))
            {
                return entryPoint == Generic
                    ? await Cbor.DeserializeAsync<T>(stream, options, token)
                    : (T)await Cbor.DeserializeAsync(typeof(T), stream, options, token);
            }
        }

        /// <summary>
        /// Reads a document that holds recognisable bytes through the same kind of stream, so that the
        /// buffer the next read rents is one that has been used and holds them.
        /// </summary>
        private static async Task PrimeAsync(string kind, string entryPoint)
        {
            RecordWithText primed = await ReadAsync<RecordWithText>(kind, entryPoint, Convert.FromHexString(PrimingDocument));
            Assert.Equal("secret", primed.S);
        }

        /// <summary>The outcome of a read, whichever way it ended, so that a test can say what leaked.</summary>
        private static async Task<(T Value, CborException Error)> AttemptAsync<T>(string kind, string entryPoint, string hex)
        {
            try
            {
                return (await ReadAsync<T>(kind, entryPoint, Convert.FromHexString(hex)), null);
            }
            catch (CborException e)
            {
                return (default, e);
            }
        }

        private static void AssertRefused<T>((T Value, CborException Error) outcome, string what)
        {
            Assert.True(outcome.Error != null, $"{what} was read as a value instead of being refused: {outcome.Value}");
            Assert.Contains("Unexpected end of buffer", outcome.Error.Message);
        }

        // ---- controls: documents that are whole must keep reading ----

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task ASmallDocumentIsReadAsync(string kind, string entryPoint)
        {
            // A1 61 41 01        {"A": 1}
            Assert.Equal(1, (await ReadAsync<RecordWithNumber>(kind, entryPoint, Convert.FromHexString("A1614101"))).A);

            // 82 01 02           [1, 2]
            Assert.Equal(new[] { 1, 2 }, await ReadAsync<int[]>(kind, entryPoint, Convert.FromHexString("820102")));

            // 00                 0
            Assert.Equal(0, await ReadAsync<int>(kind, entryPoint, Convert.FromHexString("00")));
        }

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task ADocumentAroundTheSizeOfTheFirstBufferIsReadAsync(string kind, string entryPoint)
        {
            // A non-seekable stream starts with 256 bytes and doubles when it fills, so the documents of
            // 256, 257 and 259 bytes (a byte string of 254, 255 and 256 bytes) sit on the edge of it,
            // and 100,000 bytes is several doublings past it.
            foreach (int size in new[] { 0, 1, 23, 24, 252, 253, 254, 255, 256, 257, 4096, 100_000 })
            {
                byte[] payload = Enumerable.Range(0, size).Select(i => (byte)(i * 7 + 1)).ToArray();
                ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
                Cbor.Serialize(payload, writer);

                byte[] read = await ReadAsync<byte[]>(kind, entryPoint, writer.WrittenSpan.ToArray());

                Assert.True(payload.AsSpan().SequenceEqual(read), $"a byte string of {size} bytes was not read back");
            }
        }

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task BytesAfterTheDocumentAreIgnoredAsync(string kind, string entryPoint)
        {
            // 01 02 03           1, then two more items nobody asked for
            Assert.Equal(1, await ReadAsync<int>(kind, entryPoint, Convert.FromHexString("010203")));

            // A1 61 41 01 FF FF  {"A": 1}, then bytes that are not CBOR at all
            Assert.Equal(1, (await ReadAsync<RecordWithNumber>(kind, entryPoint, Convert.FromHexString("A1614101FFFF"))).A);
        }

        // ---- a document that stops short is refused, whatever the buffer held beyond it ----

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task ATruncatedDocumentIsRefusedAsync(string kind, string entryPoint)
        {
            await PrimeAsync(kind, entryPoint);

            // A1 61 41           a map of one entry whose key is "A" and whose value is missing
            AssertRefused(await AttemptAsync<RecordWithNumber>(kind, entryPoint, "A16141"), "A1 61 41");

            // 83 01 02           an array of three items that holds two
            AssertRefused(await AttemptAsync<int[]>(kind, entryPoint, "830102"), "83 01 02");

            // 1A 00 01           an unsigned integer of four bytes that has two
            AssertRefused(await AttemptAsync<uint>(kind, entryPoint, "1A0001"), "1A 00 01");
        }

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task AnEmptyStreamIsRefusedAsync(string kind, string entryPoint)
        {
            await PrimeAsync(kind, entryPoint);

            AssertRefused(await AttemptAsync<RecordWithText>(kind, entryPoint, ""), "an empty stream");
            AssertRefused(await AttemptAsync<int>(kind, entryPoint, ""), "an empty stream");
        }

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task BytesOfAnEarlierDocumentAreNotReadAsPartOfAnotherOneAsync(string kind, string entryPoint)
        {
            // The pool hands out buffers without clearing them and another read may take the one that was
            // just primed, so the check is made more than once: each pass has to be right.
            for (int pass = 0; pass < 10; pass++)
            {
                await PrimeAsync(kind, entryPoint);

                // A1 61 53 66       {"S": <a text string of six bytes> - and the six bytes are not there
                (RecordWithText value, CborException error) = await AttemptAsync<RecordWithText>(kind, entryPoint, "A1615366");

                Assert.True(
                    value == null || value.S != "secret",
                    "the text string was completed with bytes of an earlier document");
                AssertRefused((value, error), "A1 61 53 66");
            }
        }

        [Fact]
        public async Task ADocumentThatIsReadInPiecesIsNotCompletedByTheOnePrimedBeforeAsync()
        {
            // A document shorter than the one that primed the buffer is read as itself, one byte at a
            // time: the control for the refusals above, which must not be bought by refusing this.
            await PrimeAsync("non-seekable stream, one byte per read", Generic);

            // A1 61 53 62 61 62  {"S": "ab"}
            RecordWithText value = await ReadAsync<RecordWithText>(
                "non-seekable stream, one byte per read", Generic, Convert.FromHexString("A16153626162"));

            Assert.Equal("ab", value.S);
        }

        // ---- the multiple item overloads already did this: the same answer from the same streams ----

        [Theory]
        [MemberData(nameof(EveryStreamAndEntryPoint))]
        public async Task TheMultipleItemOverloadsRefuseATruncatedDocumentTooAsync(string kind, string entryPoint)
        {
            await PrimeAsync(kind, entryPoint);

            // A1 61 41           a map of one entry whose value is missing
            byte[] bytes = Convert.FromHexString("A16141");

            using (Stream stream = Kinds[kind](bytes))
            {
                CborException error = entryPoint == Generic
                    ? await Assert.ThrowsAsync<CborException>(async () => await Cbor.DeserializeMultipleAsync<RecordWithNumber>(stream))
                    : await Assert.ThrowsAsync<CborException>(async () => await Cbor.DeserializeMultipleAsync(typeof(RecordWithNumber), stream));

                Assert.Contains("Unexpected end of buffer", error.Message);
            }
        }
    }
}
