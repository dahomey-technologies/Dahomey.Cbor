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
                return new ValueTask<T>(Deserialize(task.Result));
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
                return new ValueTask<object?>(Deserialize(task.Result));
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
                ReadOnlySequence<byte> sequence = task.Result;
                T result = Deserialize<T>(sequence.GetSpan(), options);
                reader.AdvanceTo(sequence.End);
                return new ValueTask<T>(result);
            }

            return FinishDeserializeAsync(task);

            async ValueTask<T> FinishDeserializeAsync(ValueTask<ReadOnlySequence<byte>> localTask)
            {
                ReadOnlySequence<byte> sequence = await localTask.ConfigureAwait(false);
                T result = Deserialize<T>(sequence.GetSpan(), options);
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
                ReadOnlySequence<byte> sequence = task.Result;
                object? result = Cbor.Deserialize(objectType, sequence.GetSpan(), options);
                reader.AdvanceTo(sequence.End);
                return new ValueTask<object?>(result);
            }

            return FinishDeserializeAsync(task);

            async ValueTask<object?> FinishDeserializeAsync(ValueTask<ReadOnlySequence<byte>> localTask)
            {
                ReadOnlySequence<byte> sequence = await localTask.ConfigureAwait(false);
                object? result = Cbor.Deserialize(objectType, sequence.GetSpan(), options);
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
            object input,
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
            object input, 
            Type inputType, 
            in IBufferWriter<byte> buffer, 
            CborOptions? options = null)
        {
            options ??= CborOptions.Default;
            CborWriter writer = new CborWriter(buffer);
            ICborConverter converter = options.Registry.ConverterRegistry.Lookup(inputType);
            converter.Write(ref writer, input);
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
