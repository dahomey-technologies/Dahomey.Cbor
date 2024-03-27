using System;
using System.Linq;

namespace Dahomey.Cbor.Tests.Extensions;

public static class ReadOnlySpanExtensions
{
    public static string BytesToHex(this ReadOnlySpan<byte> buffer)
    {
        return string.Concat(buffer.ToArray().Select(b => b.ToString("x2")));
    }
}
