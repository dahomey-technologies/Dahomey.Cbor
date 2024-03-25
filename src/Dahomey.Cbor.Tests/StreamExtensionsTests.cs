using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class StreamExtensionsTests
    {
        public class NoSeekStream : Stream
        {
            private ReadOnlyMemory<byte> _memory;

            public NoSeekStream(ReadOnlyMemory<byte> memory)
            {
                _memory = memory;
            }

            public override bool CanSeek => false;
            public override bool CanRead => true;

            public override int Read(byte[] buffer, int offset, int count)
            {
                Span<byte> bufferSpan = buffer.AsSpan().Slice(offset);
                int length = Math.Min(count, _memory.Length);
                _memory.Span.Slice(0, length).CopyTo(bufferSpan);
                _memory = _memory.Slice(length);
                return length;
            }

            public override bool CanWrite => throw new NotImplementedException();
            public override long Length => throw new NotImplementedException();
            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override void Flush() => throw new NotImplementedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
            public override void SetLength(long value) => throw new NotImplementedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        }

        [Theory]
        [InlineData(20, 16)]
        [InlineData(16, 16)]
        [InlineData(10, 16)]
        public async Task ReadAsyncNoSyncAsync(int bufferLength, int hintSize)
        {
            byte[] buffer = Enumerable.Range(0, bufferLength).Select(i => (byte)i).ToArray();
            NoSeekStream stream = new NoSeekStream(buffer.AsMemory());
            IMemoryOwner<byte> memory = await stream.ReadAsync(hintSize);

            Assert.True(memory.Memory.Length > bufferLength);
            Assert.Equal(memory.Memory.Span.Slice(0, bufferLength).ToArray(), buffer);

            memory.Dispose();
        }

        [Theory]
        [InlineData(20, 16)]
        [InlineData(16, 16)]
        [InlineData(10, 16)]
        public async Task ReadWwithPreciseLengthAsyncNoSyncAsync(int bufferLength, int hintSize)
        {
            byte[] buffer = Enumerable.Range(0, bufferLength).Select(i => (byte)i).ToArray();
            NoSeekStream stream = new NoSeekStream(buffer.AsMemory());
            AsyncReadResult result = await stream.ReadAndGivePreciseLengthAsync(hintSize);

            Assert.True(result.MemoryOwner.Memory.Length > bufferLength);
            Assert.Equal(result.MemoryOwner.Memory.Span.Slice(0, bufferLength).ToArray(), buffer);
            Assert.Equal(result.DataRead, bufferLength);

            result.MemoryOwner.Dispose();
        }
    }
}
