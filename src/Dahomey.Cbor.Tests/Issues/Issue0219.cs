using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// <c>ReadNextItemAsync</c> reads speculatively: an item that stops halfway through the bytes
    /// available so far cannot be told apart from one whose remaining bytes have not arrived yet, so a
    /// <see cref="CborException"/> means "retry on more data" while the pipe is open. Once the pipe is
    /// complete there is no more data to wait for, and the failure that stopped the last attempt was
    /// being replaced by a message of the method's own - so a converter's refusal was reported as a
    /// truncated stream.
    /// </summary>
    /// <remarks>
    /// The conflation predates #217: any <c>CborException</c> raised from a converter's <c>Read</c>
    /// disappeared the same way. #217 widened it to <c>[CborConstructor]</c> refusals, which until then
    /// arrived wrapped in a <c>TargetInvocationException</c> and so escaped the catch by accident. Both
    /// routes are pinned below, and this is the only site in the library that swallows a
    /// <c>CborException</c> - the other catches rethrow after enriching <see cref="CborException.Path"/>.
    /// </remarks>
    public class Issue0219
    {
        /// <summary>{"Id": 12}, a whole item - nothing about these bytes is truncated.</summary>
        private const string OneWholeItem = "A16249640C";

        /// <summary>
        /// The case the issue is about: a creator that refuses the values the document carried, read
        /// through a pipe that is already complete. <c>Cbor.Deserialize</c> on the same bytes always
        /// reported this correctly; only the sequence API lost it.
        /// </summary>
        [Fact]
        public async Task ACreatorRefusalReachesTheCallerOnACompletedPipeAsync()
        {
            PipeReader pipeReader = PipeReader.Create(new ReadOnlySequence<byte>(OneWholeItem.HexToBytes()));

            CborException exception = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.ReadNextItemAsync<RefusedByItsCreator>(pipeReader));

            Assert.StartsWith("this creator refuses 12", exception.Message);

            // Rethrown rather than wrapped, so the path collected on the way out survives too - the
            // same "$" Cbor.Deserialize reports for these bytes.
            Assert.Equal("$", exception.Path);
        }

        /// <summary>
        /// The older half of the same conflation: a converter's <c>Read</c> refusing outright. This one
        /// never involved a <c>TargetInvocationException</c>, and was swallowed here long before #217.
        /// </summary>
        [Fact]
        public async Task AConverterRefusalReachesTheCallerOnACompletedPipeAsync()
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverter(typeof(RefusedByItsConverter), new RefusingConverter());

            PipeReader pipeReader = PipeReader.Create(new ReadOnlySequence<byte>(OneWholeItem.HexToBytes()));

            CborException exception = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.ReadNextItemAsync<RefusedByItsConverter>(pipeReader, options));

            Assert.StartsWith("this converter refuses what it read", exception.Message);
            Assert.Equal("$", exception.Path);
        }

        /// <summary>
        /// The path is the root reader's doing, not the object converter's: a failure on the root value
        /// itself - a document that contradicts the requested type outright, with no member or index to
        /// name - is placed at <c>$</c> here exactly as <c>Cbor.Deserialize</c> places it.
        /// </summary>
        /// <remarks>
        /// Worth its own case because the two tests above pass either way: their types are objects, and
        /// <c>ObjectConverter</c> marks the path itself on the way out. A primitive at the root passes
        /// through no such converter, so it is the one that pins <c>RootReader</c> being on this path.
        /// </remarks>
        [Fact]
        public async Task AFailureOnTheRootValueIsPlacedAtTheRootAsync()
        {
            byte[] bytes = OneWholeItem.HexToBytes();   // a map, where an int was asked for

            CborException direct = Assert.Throws<CborException>(() => Cbor.Deserialize<int>(bytes));

            PipeReader pipeReader = PipeReader.Create(new ReadOnlySequence<byte>(bytes));
            CborException piped = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.ReadNextItemAsync<int>(pipeReader));

            Assert.Equal("$", piped.Path);
            Assert.Equal(direct.Path, piped.Path);
            Assert.Equal(direct.Message, piped.Message);
        }

        /// <summary>
        /// The read that failed is over, and the pipe knows it: a <see cref="PipeReader"/> left between
        /// a <c>ReadAsync</c> and its <c>AdvanceTo</c> answers every later call with "Reading is already
        /// in progress" instead of with the failure, so the exception this fix restores would be
        /// readable exactly once and masked from then on.
        /// </summary>
        /// <remarks>
        /// Nothing is consumed, so asking again asks the same question and gets the same answer rather
        /// than moving past the item that refused. Skipping it would mean deciding how many bytes a
        /// failed read is deemed to have eaten, which is a question for an API that offers to continue,
        /// not for the one that reports what stopped.
        /// <para>
        /// A real <see cref="Pipe"/> rather than <see cref="PipeReader.Create(ReadOnlySequence{byte})"/>
        /// because only the former tracks the read state this is about; the sequence-backed reader
        /// re-answers either way, which is why the tests above never noticed.
        /// </para>
        /// </remarks>
        [Fact]
        public async Task TheReaderIsLeftUsableAfterTheFailureAsync()
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(OneWholeItem.HexToBytes());
            await pipe.Writer.CompleteAsync();

            CborException first = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.ReadNextItemAsync<RefusedByItsCreator>(pipe.Reader));

            // Not InvalidOperationException, and not a hang: the same refusal, reported again.
            CborException second = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.ReadNextItemAsync<RefusedByItsCreator>(pipe.Reader));

            Assert.StartsWith("this creator refuses 12", first.Message);
            Assert.Equal(first.Message, second.Message);
        }

        /// <summary>
        /// A stream that really is truncated still fails, and now says so in the reader's own words
        /// rather than in a message invented one frame up. The distinction the fix restores is between
        /// two failures that were both reported as this one.
        /// </summary>
        [Fact]
        public async Task ATruncatedItemReportsTheReadThatRanOutOfBytesAsync()
        {
            // {"Id": ... - a map of one pair whose value never arrives.
            PipeReader pipeReader = PipeReader.Create(new ReadOnlySequence<byte>("A1624964".HexToBytes()));

            CborException exception = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.ReadNextItemAsync<Plain>(pipeReader));

            Assert.Contains("Unexpected end of buffer", exception.Message);
        }

        /// <summary>
        /// While the pipe is open the behaviour is unchanged: a refusal is still read as "not enough
        /// bytes yet" and the read keeps waiting, because on an open pipe the two are genuinely
        /// indistinguishable. The refusal surfaces at the moment the writer completes - not before.
        /// </summary>
        [Fact]
        public async Task AnOpenPipeStillWaitsAndOnlyReportsTheRefusalOnceItCompletesAsync()
        {
            Pipe pipe = new Pipe();
            await pipe.Writer.WriteAsync(OneWholeItem.HexToBytes());

            Task<RefusedByItsCreator> reading = Cbor.ReadNextItemAsync<RefusedByItsCreator>(pipe.Reader).AsTask();

            // The whole item is in the pipe and the creator has already refused it once. The read is
            // waiting for bytes that would change its mind rather than reporting anything.
            Assert.NotSame(reading, await Task.WhenAny(reading, Task.Delay(TimeSpan.FromMilliseconds(250))));

            await pipe.Writer.CompleteAsync();

            // The read was started here a few lines up, which is what VSTHRD003 is asking about; it is
            // held rather than awaited only so that its not-completing can be asserted first.
#pragma warning disable VSTHRD003
            CborException exception = await Assert.ThrowsAsync<CborException>(() => reading);
#pragma warning restore VSTHRD003
            Assert.StartsWith("this creator refuses 12", exception.Message);
        }

        public class Plain
        {
            public int Id { get; set; }
        }

        public class RefusedByItsCreator
        {
            public int Id { get; set; }

            [CborConstructor]
            public RefusedByItsCreator(int id)
            {
                throw new CborException($"this creator refuses {id}");
            }
        }

        public class RefusedByItsConverter
        {
        }

        public class RefusingConverter : CborConverterBase<RefusedByItsConverter>
        {
            public override RefusedByItsConverter Read(ref CborReader reader)
            {
                throw new CborException("this converter refuses what it read");
            }

            public override void Write(ref CborWriter writer, RefusedByItsConverter value)
                => throw new NotSupportedException();
        }
    }
}
