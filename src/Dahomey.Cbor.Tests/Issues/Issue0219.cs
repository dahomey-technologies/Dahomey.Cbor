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

            // Rethrown rather than wrapped, so the path the converters enriched on the way out survives
            // too - the same "$" Cbor.Deserialize reports for these bytes.
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

            Assert.Equal("this converter refuses what it read", exception.Message);
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
