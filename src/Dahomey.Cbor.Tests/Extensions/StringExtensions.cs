using System;
using System.Linq;

namespace Dahomey.Cbor.Tests.Extensions;

public static class StringExtensions
{
    public static byte[] HexToBytes(this string hexBuffer)
    {
        return hexBuffer
            .Select((c, i) => new { c, i })
            .GroupBy(a => a.i / 2)
            .Select(grp => new string(grp.Select(g => g.c).ToArray()))
            .Select(value => Convert.ToByte(value, 16)).ToArray();
    }
}
