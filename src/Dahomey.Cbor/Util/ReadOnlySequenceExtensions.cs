using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Util
{
    public static class ReadOnlySequenceExtensions
    {
        public static ReadOnlySpan<T> GetSpan<T>(this ReadOnlySequence<T> sequence)
        {
            if (sequence.IsSingleSegment)
            {
                return sequence.First.Span;
            }

            return sequence.ToArray();
        }
    }
}
