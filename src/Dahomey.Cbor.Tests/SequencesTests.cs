using Dahomey.Cbor.Serialization;
using Nerdbank.Streams;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class SequencesTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task CanReadAsync(int count)
        {
            using var sequence = new Sequence<byte>();
            WriteItems(sequence, count);


            var pipeReader = PipeReader.Create(sequence.AsReadOnlySequence);

            for (var i = 0; i < count; i++)
            {
                var result = await Cbor.ReadNextItemAsync<int?>(pipeReader);
                Assert.Equal(i, result);
            }

            var last = await Cbor.ReadNextItemAsync<int?>(pipeReader);
            Assert.Null(last);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task CanReadFragmentizedAsync(int count)
        {
            using var sequence = new Sequence<byte>();
            WriteItems(sequence, count);
            var framentized = Helper.Fragmentize(sequence.AsReadOnlySequence.ToArray());

            var pipeReader = PipeReader.Create(framentized);

            for (var i = 0; i < count; i++)
            {
                var result = await Cbor.ReadNextItemAsync<int?>(pipeReader);
                Assert.Equal(i, result);
            }

            var last = await Cbor.ReadNextItemAsync<int?>(pipeReader);
            Assert.Null(last);
        }

        private static void WriteItems(IBufferWriter<byte> bufferWriter, int count)
        {
            var writer = new CborWriter(bufferWriter);

            for (var i = 0; i < count; i++)
            {
                writer.WriteInt32(i);
            }
        }
    }
}
