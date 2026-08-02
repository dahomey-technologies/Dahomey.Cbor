using System;

namespace Dahomey.Cbor.Serialization
{
    /// <summary>
    /// RFC 8949 section 4.2.1 map key ordering: bytewise lexicographic on the encoded key.
    /// </summary>
    /// <remarks>
    /// This is the single definition of deterministic key order. It is not the deprecated
    /// section 4.2.3 length-first variant, which is not implemented.
    /// </remarks>
    public static class CborKeyComparer
    {
        /// <param name="a">Raw UTF-8 member name, without the CBOR text-string header.</param>
        /// <param name="b">Raw UTF-8 member name, without the CBOR text-string header.</param>
        public static int CompareTextKeys(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            // The encoded key is header || bytes, and the header encodes the length, so a shorter
            // encoded key always sorts before a longer one. Comparing encoded headers reduces to
            // comparing the encoded length of each key.
            int headerComparison = CompareArgumentEncoding((ulong)a.Length, (ulong)b.Length);

            if (headerComparison != 0)
            {
                return headerComparison;
            }

            return a.SequenceCompareTo(b);
        }

        public static int CompareIntKeys(int a, int b)
        {
            // Non-negative keys are major type 0 and encode monotonically, so numeric order is
            // bytewise order. Negative keys are major type 1, whose leading byte is always greater
            // than any major type 0 leading byte, so every negative key sorts after every
            // non-negative one. Within the negatives, -1 encodes as 0x20 and -2 as 0x21, so
            // descending magnitude is ascending bytewise.
            if (a >= 0 && b >= 0)
            {
                return a.CompareTo(b);
            }

            if (a < 0 && b < 0)
            {
                ulong argumentA = (ulong)(-1L - a);
                ulong argumentB = (ulong)(-1L - b);
                return CompareArgumentEncoding(argumentA, argumentB) is int c and not 0
                    ? c
                    : argumentA.CompareTo(argumentB);
            }

            return a >= 0 ? -1 : 1;
        }

        /// <summary>
        /// Compares two arguments by the encoded form the shortest-form ladder gives them: first by
        /// how many bytes they occupy, then by value.
        /// </summary>
        private static int CompareArgumentEncoding(ulong a, ulong b)
        {
            int sizeComparison = ArgumentEncodedSize(a).CompareTo(ArgumentEncodedSize(b));
            return sizeComparison != 0 ? sizeComparison : a.CompareTo(b);
        }

        private static int ArgumentEncodedSize(ulong value)
        {
            if (value < 24) return 1;
            if (value <= byte.MaxValue) return 2;
            if (value <= ushort.MaxValue) return 3;
            if (value <= uint.MaxValue) return 5;
            return 9;
        }
    }
}
