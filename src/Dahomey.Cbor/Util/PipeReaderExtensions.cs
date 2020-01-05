using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines
{
    public static class PipeReaderExtensions
    {
        public static ValueTask<ReadOnlySequence<byte>> FullReadAsync(this PipeReader reader, CancellationToken token = default)
        {
            // Attempts to synchronously read data first
            if (reader.TryRead(out ReadResult result) && result.IsCompleted)
            {
                return new ValueTask<ReadOnlySequence<byte>>(result.Buffer);
            }

            reader.AdvanceTo(result.Buffer.Start);

            // Otherwise read asynchronously
            return FinishReadAsync();

            async ValueTask<ReadOnlySequence<byte>> FinishReadAsync()
            {
                while (true)
                {
                    result = await reader.ReadAsync(token);
                    if (result.IsCompleted)
                    {
                        return result.Buffer;
                    }

                    reader.AdvanceTo(result.Buffer.Start);
                }
            }
        }
    }
}
