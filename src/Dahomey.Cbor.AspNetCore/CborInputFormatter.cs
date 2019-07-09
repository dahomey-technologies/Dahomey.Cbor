using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace Dahomey.Cbor.AspNetCore
{
    public class CborInputFormatter : InputFormatter
    {
        private readonly static Task<InputFormatterResult> failureTask = Task.FromResult(InputFormatterResult.Failure());
        private readonly static Task<InputFormatterResult> noValueTask = Task.FromResult(InputFormatterResult.NoValue());

        private readonly CborOptions _cborOptions;

        public CborInputFormatter(CborOptions cborOptions)
        {
            _cborOptions = cborOptions;

            SupportedMediaTypes.Add("application/cbor");
        }

        public override Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            try
            {
                ValueTask<IMemoryOwner<byte>> task = ReadAsync(context.HttpContext.Request.Body);
                if (task.IsCompletedSuccessfully)
                {
                    using (task.Result)
                    {
                        object model = Deserialize(context.ModelType, task.Result.Memory.Span);
                        return Task.FromResult(InputFormatterResult.Success(model));
                    }
                }

                return FinishReadRequestBodyAsync(context.ModelType, task);
            }
            catch (Exception ex)
            {
                context.ModelState.AddModelError("CBOR", ex.Message);
                return failureTask;
            }

            async Task<InputFormatterResult> FinishReadRequestBodyAsync(Type objectType, ValueTask<IMemoryOwner<byte>> task)
            {
                await task;
                using (task.Result)
                {
                    object model = Deserialize(objectType, task.Result.Memory.Span);
                    return InputFormatterResult.Success(model);
                }
            }
        }

        private object Deserialize(Type objectType, ReadOnlySpan<byte> buffer)
        {
            CborReader reader = new CborReader(buffer, _cborOptions);
            ICborConverter cborConverter = CborConverter.Lookup(objectType);
            return cborConverter.Read(ref reader);

        }

        private async ValueTask<IMemoryOwner<byte>> ReadAsync(Stream stream)
        {
            var totalSize = 0;
            IMemoryOwner<byte> buffer = MemoryPool<byte>.Shared.Rent();
            int read;
            while ((read = await stream.ReadAsync(buffer.Memory)) > 0)
            {
                if (totalSize + read == buffer.Memory.Length)
                {
                    IMemoryOwner<byte> backup = buffer;
                    buffer = MemoryPool<byte>.Shared.Rent(backup.Memory.Length * 2);
                    backup.Memory.Span.CopyTo(buffer.Memory.Span);
                    backup.Dispose();
                }

                totalSize += read;
            }

            return buffer;
        }
    }
}
