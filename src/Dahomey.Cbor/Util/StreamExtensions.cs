using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

#if NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace System.IO
{
    public class AsyncReadResult
    {
        public IMemoryOwner<byte>? MemoryOwner { get; set; }

        public int DataRead { get; set; }
    }

    public static class StreamExtensions
    {
#if NETSTANDARD2_0
        internal static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
            {
                return new ValueTask<int>(stream.ReadAsync(array.Array, array.Offset, array.Count, cancellationToken));
            }
            else
            {
                byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
                return FinishReadAsync(stream.ReadAsync(sharedBuffer, 0, buffer.Length, cancellationToken), sharedBuffer, buffer);

                static async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
                {
                    try
                    {
                        int result = await readTask.ConfigureAwait(false);
                        new Span<byte>(localBuffer, 0, result).CopyTo(localDestination.Span);
                        return result;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(localBuffer);
                    }
                }
            }
        }
#endif

        /// <summary>
        /// A single read is allowed to return fewer bytes than were asked for, however seekable the
        /// stream is, so it is repeated until the destination is filled or the stream ends. Returns the
        /// number of bytes actually read.
        /// </summary>
        private static async ValueTask<int> ReadUpToAsync(Stream stream, Memory<byte> destination, CancellationToken cancellationToken)
        {
            int totalRead = 0;

            while (totalRead < destination.Length)
            {
                int read = await stream.ReadAsync(destination.Slice(totalRead), cancellationToken);

                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        public static async ValueTask<AsyncReadResult> ReadAndGivePreciseLengthAsync(this Stream stream, int sizeHint, CancellationToken cancellationToken = default)
        {
            AsyncReadResult result = new AsyncReadResult();

            if (stream.CanSeek)
            {
                int length = (int)stream.Length;
                result.MemoryOwner = MemoryPool<byte>.Shared.Rent(length);
                result.DataRead = await ReadUpToAsync(stream, result.MemoryOwner.Memory.Slice(0, length), cancellationToken);
                return result;
            }

            int totalSize = 0;
            int read;
            result.MemoryOwner = MemoryPool<byte>.Shared.Rent(sizeHint);

            while ((read = await stream.ReadAsync(result.MemoryOwner.Memory.Slice(totalSize), cancellationToken)) > 0)
            {
                if (totalSize + read == result.MemoryOwner.Memory.Length)
                {
                    using IMemoryOwner<byte> oldBuffer = result.MemoryOwner;
                    result.MemoryOwner = MemoryPool<byte>.Shared.Rent(oldBuffer.Memory.Length * 2);
                    oldBuffer.Memory.CopyTo(result.MemoryOwner.Memory);
                }

                totalSize += read;
            }

            result.DataRead = totalSize;
            return result;
        }

        public static async ValueTask<IMemoryOwner<byte>> ReadAsync(this Stream stream, int sizeHint, CancellationToken cancellationToken = default)
        {
            IMemoryOwner<byte> buffer;

            if (stream.CanSeek)
            {
                int length = (int)stream.Length;
                buffer = MemoryPool<byte>.Shared.Rent(length);
                await ReadUpToAsync(stream, buffer.Memory.Slice(0, length), cancellationToken);
                return buffer;
            }

            int totalSize = 0;
            int read;
            buffer = MemoryPool<byte>.Shared.Rent(sizeHint);

            while ((read = await stream.ReadAsync(buffer.Memory.Slice(totalSize), cancellationToken)) > 0)
            {
                if (totalSize + read == buffer.Memory.Length)
                {
                    using IMemoryOwner<byte> oldBuffer = buffer;
                    buffer = MemoryPool<byte>.Shared.Rent(oldBuffer.Memory.Length * 2);
                    oldBuffer.Memory.CopyTo(buffer.Memory);
                }

                totalSize += read;
            }

            return buffer;
        }
    }
}
