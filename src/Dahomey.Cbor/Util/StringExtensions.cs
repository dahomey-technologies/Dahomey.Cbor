using System;
using System.Text;

namespace Dahomey.Cbor.Util
{
    public static class StringExtensions
    {
        public static ReadOnlySpan<byte> AsBinarySpan(this string str, Encoding? encoding = null)
        {
            Encoding actualEncoding = encoding ?? Encoding.UTF8;
            return actualEncoding.GetBytes(str);
        }

        public static ReadOnlyMemory<byte> AsBinaryMemory(this string str, Encoding? encoding = null)
        {
            Encoding actualEncoding = encoding ?? Encoding.UTF8;
            return actualEncoding.GetBytes(str);
        }
    }
}
