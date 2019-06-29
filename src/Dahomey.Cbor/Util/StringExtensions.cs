using System;
using System.Buffers;
using System.Text;

namespace Dahomey.Cbor.Util
{
    public static class StringExtensions
    {
        public static ReadOnlySpan<byte> AsBinarySpan(this string str, Encoding encoding = null)
        {
            Encoding actualEncoding = encoding ?? Encoding.UTF8;
            int byteCount = actualEncoding.GetByteCount(str);
            IMemoryOwner<byte> bytes = MemoryPool<byte>.Shared.Rent(byteCount);
            Span<byte> span = bytes.Memory.Span;
            actualEncoding.GetBytes(str.AsSpan(), span);
            return span;
        }

        public static ReadOnlyMemory<byte> AsBinaryMemory(this string str, Encoding encoding = null)
        {
            Encoding actualEncoding = encoding ?? Encoding.UTF8;
            return actualEncoding.GetBytes(str);
        }
    }
}
