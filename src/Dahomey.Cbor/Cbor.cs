using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Dahomey.Cbor
{
    public static class Cbor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T> DeserializeAsync<T>(
            Stream stream,
            CborOptions? options = null,
            CancellationToken token = default)
        {
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);
                return new ValueTask<T>(Deserialize<T>(span, options));
            }

            ValueTask<IMemoryOwner<byte>> task = stream.ReadAsync(256, token);

            if (task.IsCompletedSuccessfully)
            {
#pragma warning disable VSTHRD103
                return new ValueTask<T>(Deserialize(task.Result));
#pragma warning restore VSTHRD103
            }

            return FinishDeserializeAsync(task);

            T Deserialize(IMemoryOwner<byte> memoryOwner)
            {
                using (memoryOwner)
                {
                    return Deserialize<T>(memoryOwner.Memory.Span, options);
                }
            }

            async ValueTask<T> FinishDeserializeAsync(ValueTask<IMemoryOwner<byte>> localTask)
            {
                return Deserialize(await localTask.ConfigureAwait(false));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<object?> DeserializeAsync(
            Type objectType,
            Stream stream,
            CborOptions? options = null,
            CancellationToken token = default)
        {
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);
                return new ValueTask<object?>(Cbor.Deserialize(objectType, span, options));
            }

            ValueTask<IMemoryOwner<byte>> task = stream.ReadAsync(256, token);

            if (task.IsCompletedSuccessfully)
            {
#pragma warning disable VSTHRD103
                return new ValueTask<object?>(Deserialize(task.Result));
#pragma warning restore VSTHRD103
            }

            return FinishDeserializeAsync(task);

            object? Deserialize(IMemoryOwner<byte> memoryOwner)
            {
                using (memoryOwner)
                {
                    return Cbor.Deserialize(objectType, memoryOwner.Memory.Span, options);
                }
            }

            async ValueTask<object?> FinishDeserializeAsync(ValueTask<IMemoryOwner<byte>> localTask)
            {
                return Deserialize(await localTask.ConfigureAwait(false));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T> DeserializeAsync<T>(
            PipeReader reader, 
            CborOptions? options = null,
            CancellationToken token = default)
        {
            ValueTask<ReadOnlySequence<byte>> task = reader.FullReadAsync(token);

            if (task.IsCompletedSuccessfully)
            {
#pragma warning disable VSTHRD103
                ReadOnlySequence<byte> sequence = task.Result;
#pragma warning restore VSTHRD103
                T result = Deserialize<T>(sequence, options);
                reader.AdvanceTo(sequence.End);
                return new ValueTask<T>(result);
            }

            return FinishDeserializeAsync(task);

            async ValueTask<T> FinishDeserializeAsync(ValueTask<ReadOnlySequence<byte>> localTask)
            {
                ReadOnlySequence<byte> sequence = await localTask.ConfigureAwait(false);
                T result = Deserialize<T>(sequence, options);
                reader.AdvanceTo(sequence.End);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<object?> DeserializeAsync(
            Type objectType, 
            PipeReader reader, 
            CborOptions? options = null,
            CancellationToken token = default)
        {
            ValueTask<ReadOnlySequence<byte>> task = reader.FullReadAsync(token);

            if (task.IsCompletedSuccessfully)
            {
#pragma warning disable VSTHRD103
                ReadOnlySequence<byte> sequence = task.Result;
#pragma warning restore VSTHRD103
                object? result = Cbor.Deserialize(objectType, sequence, options);
                reader.AdvanceTo(sequence.End);
                return new ValueTask<object?>(result);
            }

            return FinishDeserializeAsync(task);

            async ValueTask<object?> FinishDeserializeAsync(ValueTask<ReadOnlySequence<byte>> localTask)
            {
                ReadOnlySequence<byte> sequence = await localTask.ConfigureAwait(false);
                object? result = Cbor.Deserialize(objectType, sequence, options);
                reader.AdvanceTo(sequence.End);
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(
            ReadOnlySpan<byte> buffer, 
            CborOptions? options = null)
        {
            options ??= CborOptions.Default;
            CborReader reader = new CborReader(buffer);
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            return converter.Read(ref reader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(
            ReadOnlySequence<byte> buffer,
            CborOptions? options = null)
        {
            options ??= CborOptions.Default;
            CborReader reader = new CborReader(buffer);
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            return converter.Read(ref reader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? Deserialize(
            Type objectType, 
            ReadOnlySpan<byte> buffer,
            CborOptions? options = null)
        {
            options ??= CborOptions.Default;
            CborReader reader = new CborReader(buffer);
            ICborConverter cborConverter = options.Registry.ConverterRegistry.Lookup(objectType);
            return cborConverter.Read(ref reader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object? Deserialize(
            Type objectType,
            ReadOnlySequence<byte> buffer,
            CborOptions? options = null)
        {
            options ??= CborOptions.Default;
            CborReader reader = new CborReader(buffer);
            ICborConverter cborConverter = options.Registry.ConverterRegistry.Lookup(objectType);
            return cborConverter.Read(ref reader);
        }

        /// <summary>
        /// Deserializes the CBOR buffer to the given anonymous type.
        /// </summary>
        /// <typeparam name="T">The anonymous type to deserialize to. This can't be specified directly and will be inferred from the anonymous type passed as a parameter.</typeparam>
        /// <param name="buffer">The CBOR buffer to deserialize.</param>
        /// <param name="anonymousTypeObject">The anonymous type object.</param>
        /// <param name="options">The options used to deserialize the object. If this is null, default options will be used.</param>
        /// <returns>The deserialized anonymous type from the CBOR buffer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
        public static T DeserializeAnonymousType<T>(
            ReadOnlySpan<byte> buffer, T anonymousTypeObject, CborOptions? options = null)
        {
            return Deserialize<T>(buffer, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task SerializeAsync<T>(
            T input, 
            Stream stream, 
            CborOptions? options = null,
            CancellationToken token = default)
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();
            Serialize(input, bufferWriter, options);
            return bufferWriter.CopyToAsync(stream, token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task SerializeAsync(
            object? input,
            Type inputType,
            Stream stream,
            CborOptions? options = null,
            CancellationToken token = default)
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();
            Serialize(input, inputType, bufferWriter, options);
            return bufferWriter.CopyToAsync(stream, token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(
            T input, 
            in IBufferWriter<byte> buffer, 
            CborOptions? options = null)
        {
            options ??= CborOptions.Default;
            CborWriter writer = new CborWriter(buffer);
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            converter.Write(ref writer, input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize(
            object? input, 
            Type inputType, 
            in IBufferWriter<byte> buffer, 
            CborOptions? options = null)
        {
            CborWriter writer = new CborWriter(buffer);
            if (input is null)
            {
                writer.WriteNull();
            }
            else
            {
                options ??= CborOptions.Default;
                ICborConverter converter = options.Registry.ConverterRegistry.Lookup(inputType);
                converter.Write(ref writer, input);
            }
        }

        /// <summary>
        /// Deserialize the next item of a CBOR sequence.
        /// </summary>
        /// <typeparam name="TItem">The type of item to read</typeparam>
        /// <param name="reader">The pipe reader to read the sequence from</param>
        /// <param name="options">The cbor options</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The next item in the sequence. default(TItem) is returned when no more items are available</returns>
        /// <exception cref="CborException">Thrown if the reader does not contain a valid cbor sequence</exception>
        public static async ValueTask<TItem?> ReadNextItemAsync<TItem>(PipeReader reader, CborOptions? options = null, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken: cancellationToken);
                if (result.IsCanceled)
                {
                    await Task.FromCanceled(cancellationToken);
                }

                ReadOnlySequence<byte> buffer = result.Buffer;
                if (buffer.IsEmpty)
                {
                    return default;
                }

                try
                {
                    if (TryReadItem(buffer, options ?? CborOptions.Default, out TItem? item, out var consumed))
                    {
                        reader.AdvanceTo(buffer.GetPosition(offset: consumed), examined: buffer.End);
                        result = default;
                        return item;
                    }
                    else
                    {
                        if (result.IsCompleted)
                        {
                            throw new CborException("Reader has completed with some buffer bytes but no item was read");
                        }

                        reader.AdvanceTo(buffer.Start, examined: buffer.End);
                        result = default;
                    }
                }
                catch (Exception) when (!result.IsCompleted)
                {
                    // Eat exception, as there is more data in the pipe, and we want to try again.
                    // This is kind of naive, as the exception could be thrown either because
                    // the object is not complete -or- because something actually went
                    // wrong (IOException)
                    // 
                    // Anyways, if the pipe is complete, the exception will not be catched (see filter)

                    reader.AdvanceTo(buffer.Start, examined: buffer.End);
                    result = default;
                }
            }

            static bool TryReadItem(
                in ReadOnlySequence<byte> sequence,
                CborOptions options,
                out TItem? item,
                out int consumed)
            {
                CborReader reader = new CborReader(sequence);
                ICborConverter<TItem> converter = options.Registry.ConverterRegistry.Lookup<TItem>();

                try
                {
                    item = converter.Read(ref reader);
                    consumed = reader.GetBookmark().currentPos;
                    return true;
                }
                catch (CborException)
                {
                    item = default;
                    consumed = 0;
                    return false;
                }
            }
        }

        /// <summary>
        /// Converts a CBOR binary buffer to a Json string for debugging purposes
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static string? ToJson(
            ReadOnlySpan<byte> buffer,
            CborOptions? options = null)
        {
            return Deserialize<CborValue>(buffer, options)?.ToString();
        }
    }
}
