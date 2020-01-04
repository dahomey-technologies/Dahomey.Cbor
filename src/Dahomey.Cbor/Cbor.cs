using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using System;
using System.Buffers;
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
            CborOptions options = null)
        {
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);
                return new ValueTask<T>(Deserialize<T>(span, options));
            }

            ValueTask<(byte[], int)> task = stream.CanSeek
                ? ReadStreamFullAsync(stream)
                : ReadAsync(stream, 256);

            if (task.IsCompletedSuccessfully)
            {
                (byte[] bytes, int size) = task.Result;
                return new ValueTask<T>(Deserialize(bytes, size));
            }

            return FinishDeserializeAsync(task);

            T Deserialize(byte[] bytes, int size)
            {
                Span<byte> span = new Span<byte>(bytes, 0, size);
                T result = Deserialize<T>(span, options);
                ArrayPool<byte>.Shared.Return(bytes);
                return result;
            }

            async ValueTask<T> FinishDeserializeAsync(ValueTask<(byte[], int)> localTask)
            {
                (byte[] bytes, int size) = await localTask.ConfigureAwait(false);
                return Deserialize(bytes, size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<object> DeserializeAsync(
            Type objectType,
            Stream stream,
            CborOptions options = null)
        {
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);
                return new ValueTask<object>(Cbor.Deserialize(objectType, span, options));
            }

            ValueTask<(byte[], int)> task = stream.CanSeek
                ? ReadStreamFullAsync(stream)
                : ReadAsync(stream, 256);

            if (task.IsCompletedSuccessfully)
            {
                (byte[] bytes, int size) = task.Result;
                return new ValueTask<object>(Deserialize(bytes, size));
            }

            return FinishDeserializeAsync(task);

            object Deserialize(byte[] bytes, int size)
            {
                Span<byte> span = new Span<byte>(bytes, 0, size);
                object result = Cbor.Deserialize(objectType, span, options);
                ArrayPool<byte>.Shared.Return(bytes);
                return result;
            }

            async ValueTask<object> FinishDeserializeAsync(ValueTask<(byte[], int)> localTask)
            {
                (byte[] bytes, int size) = await localTask.ConfigureAwait(false);
                return Deserialize(bytes, size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T> DeserializeAsync<T>(PipeReader reader, CborOptions options = null)
        {
            if (reader.TryRead(out ReadResult result) && result.IsCompleted)
            {
                T obj = Deserialize<T>(result.Buffer.GetSpan(), options);
                reader.AdvanceTo(result.Buffer.End);
                return new ValueTask<T>(obj);
            }

            reader.AdvanceTo(result.Buffer.Start);

            ValueTask<ReadOnlySequence<byte>> task = ReadAsync(reader);

            if (task.IsCompletedSuccessfully)
            {
                ReadOnlySequence<byte> sequence = task.Result;
                return new ValueTask<T>(Deserialize<T>(sequence.GetSpan(), options));
            }

            return FinishDeserializeAsync(task);

            async ValueTask<T> FinishDeserializeAsync(ValueTask<ReadOnlySequence<byte>> localTask)
            {
                ReadOnlySequence<byte> sequence = await localTask.ConfigureAwait(false);
                return Deserialize<T>(sequence.GetSpan(), options);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<object> DeserializeAsync(Type objectType, PipeReader reader, CborOptions options = null)
        {
            if (reader.TryRead(out ReadResult result) && result.IsCompleted)
            {
                object obj = Deserialize(objectType, result.Buffer.GetSpan(), options);
                reader.AdvanceTo(result.Buffer.End);
                return new ValueTask<object>(obj);
            }

            reader.AdvanceTo(result.Buffer.Start);

            ValueTask<ReadOnlySequence<byte>> task = ReadAsync(reader);

            if (task.IsCompletedSuccessfully)
            {
                ReadOnlySequence<byte> sequence = task.Result;
                return new ValueTask<object>(Deserialize(objectType, sequence.GetSpan(), options));
            }

            return FinishDeserializeAsync(task);

            async ValueTask<object> FinishDeserializeAsync(ValueTask<ReadOnlySequence<byte>> localTask)
            {
                ReadOnlySequence<byte> sequence = await localTask.ConfigureAwait(false);
                return Deserialize(objectType, sequence.GetSpan(), options);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async ValueTask<ReadOnlySequence<byte>> ReadAsync(PipeReader reader)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                if (result.IsCompleted)
                {
                    return result.Buffer;
                }

                reader.AdvanceTo(result.Buffer.Start);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(
            ReadOnlySpan<byte> buffer, 
            CborOptions options = null)
        {
            options = options ?? CborOptions.Default;
            CborReader reader = new CborReader(buffer, options);
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            return converter.Read(ref reader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Deserialize(
            Type objectType, 
            ReadOnlySpan<byte> buffer,
            CborOptions options = null)
        {
            options = options ?? CborOptions.Default;
            CborReader reader = new CborReader(buffer, options);
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
        public static T DeserializeAnonymousType<T>(
            ReadOnlySpan<byte> buffer, T anonymousTypeObject, CborOptions options = null)
        {
            return Deserialize<T>(buffer, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task SerializeAsync<T>(
            T input, 
            Stream stream, 
            CborOptions options = null)
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Serialize(input, bufferWriter, options);
                return bufferWriter.CopyToAsync(stream);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task SerializeAsync(
            object input,
            Type inputType,
            Stream stream,
            CborOptions options = null)
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Serialize(input, inputType, bufferWriter, options);
                return bufferWriter.CopyToAsync(stream);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(T input, in IBufferWriter<byte> buffer, CborOptions options = null)
        {
            options = options ?? CborOptions.Default;
            CborWriter writer = new CborWriter(buffer, options);
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            converter.Write(ref writer, input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize(object input, Type inputType, in IBufferWriter<byte> buffer, CborOptions options = null)
        {
            options = options ?? CborOptions.Default;
            CborWriter writer = new CborWriter(buffer, options);
            ICborConverter converter = options.Registry.ConverterRegistry.Lookup(inputType);
            converter.Write(ref writer, input);
        }

        /// <summary>
        /// Converts a CBOR binary buffer to a Json string for debugging purposes
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static string ToJson(
            ReadOnlySpan<byte> buffer,
            CborOptions options = null)
        {
            return Deserialize<CborValue>(buffer, options).ToString();
        }

        private static async ValueTask<(byte[], int)> ReadStreamFullAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            return (buffer, read);
        }

        private static async ValueTask<(byte[], int)> ReadAsync(Stream stream, int sizeHint)
        {
            var totalSize = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
            int read;
            while ((read = await stream.ReadAsync(buffer, totalSize, buffer.Length - totalSize)) > 0)
            {
                if (totalSize + read == buffer.Length)
                {
                    byte[] backup = buffer;
                    buffer = ArrayPool<byte>.Shared.Rent(backup.Length * 2);
                    backup.AsSpan().CopyTo(buffer);
                    ArrayPool<byte>.Shared.Return(backup);
                }

                totalSize += read;
            }

            return (buffer, totalSize);
        }
    }
}
