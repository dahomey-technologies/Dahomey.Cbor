using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace Dahomey.Cbor
{
    public static class CborSerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(
            Stream stream,
            CborSerializationSettings settings = null)
        {
            if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(buffer.Array, buffer.Offset, buffer.Count);
                return Deserialize<T>(span, settings);
            }

            (byte[] bytes, int size) = stream.CanSeek
                ? ReadStreamFull(stream)
                : ReadStream(stream, 256);

            Memory<byte> memory = new Memory<byte>(bytes, 0, size);
            T result = Deserialize<T>(memory.Span, settings);
            ArrayPool<byte>.Shared.Return(bytes);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Deserialize<T>(
            ReadOnlySpan<byte> buffer, 
            CborSerializationSettings settings = null)
        {
            CborReader reader = new CborReader(buffer, settings);
            ICborConverter<T> converter = CborConverter.Lookup<T>();
            return converter.Read(ref reader);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(
            T input, 
            Stream stream, 
            CborSerializationSettings settings = null)
        {
            ByteBufferWriter bufferWriter = new ByteBufferWriter();
            CborWriter writer = new CborWriter(bufferWriter, settings);
            ICborConverter<T> converter = CborConverter.Lookup<T>();
            converter.Write(ref writer, input);
            ReadOnlySpan<byte> span = bufferWriter.WrittenSpan;
            stream.Write(span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Serialize<T>(T input, in IBufferWriter<byte> buffer, CborSerializationSettings settings = null)
        {
            CborWriter writer = new CborWriter(buffer, settings);
            ICborConverter<T> converter = CborConverter.Lookup<T>();
            converter.Write(ref writer, input);
        }

        private static (byte[], int) ReadStreamFull(Stream stream)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);
            int read = stream.Read(buffer, 0, buffer.Length);
            return (buffer, read);
        }

        private static (byte[], int) ReadStream(Stream stream, int sizeHint)
        {
            var totalSize = 0;
            var buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
            int read;
            while ((read = stream.Read(buffer, totalSize, buffer.Length - totalSize)) > 0)
            {
                if (totalSize + read == buffer.Length)
                {
                    GrowArray(ref buffer);
                }

                totalSize += read;
            }

            return (buffer, totalSize);
        }

        private static void GrowArray<T>(ref T[] array)
        {
            var backup = array;
            array = ArrayPool<T>.Shared.Rent(backup.Length * 2);
            backup.AsSpan().CopyTo(array);
            ArrayPool<T>.Shared.Return(backup);
        }
    }
}
