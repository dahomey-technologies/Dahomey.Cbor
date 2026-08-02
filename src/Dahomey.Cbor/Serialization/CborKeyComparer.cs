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
            return CompareContentKeys(a, b);
        }

        /// <summary>
        /// Compares two CBOR map keys of any supported kind by their encoded form: by major type
        /// first, then within a major type by the rule for that type. Major type order is the byte
        /// order of the leading byte, which is what puts unsigned (0) before negative (1) before byte
        /// string (2) before text string (3).
        /// </summary>
        /// <remarks>
        /// Each side is described by a major type plus the data that varies within it, so one method
        /// covers both a map whose keys are all one kind and a map that mixes kinds — which a
        /// <see cref="Dahomey.Cbor.ObjectModel.CborObject"/> read off the wire is free to do.
        /// </remarks>
        /// <param name="majorTypeA">Key A's CBOR major type.</param>
        /// <param name="argumentA">
        /// Key A's CBOR argument when <paramref name="majorTypeA"/> is an integer type, as defined by
        /// <see cref="CompareIntegerKeys"/>. Ignored for string types, whose argument is their content
        /// length.
        /// </param>
        /// <param name="contentA">
        /// Key A's raw payload when <paramref name="majorTypeA"/> is a string type: UTF-8 bytes for
        /// text, the bytes themselves for a byte string, in both cases without the CBOR header.
        /// Ignored for integer types.
        /// </param>
        /// <param name="majorTypeB">Key B's CBOR major type.</param>
        /// <param name="argumentB">Key B's CBOR argument, as above.</param>
        /// <param name="contentB">Key B's raw payload, as above.</param>
        public static int CompareKeys(
            CborMajorType majorTypeA, ulong argumentA, ReadOnlySpan<byte> contentA,
            CborMajorType majorTypeB, ulong argumentB, ReadOnlySpan<byte> contentB)
        {
            if (majorTypeA != majorTypeB)
            {
                return majorTypeA < majorTypeB ? -1 : 1;
            }

            switch (majorTypeA)
            {
                case CborMajorType.PositiveInteger:
                case CborMajorType.NegativeInteger:
                    // Same major type on both sides, so the leading byte's high bits agree and only the
                    // argument distinguishes them -- exactly what CompareIntegerKeys does once it has
                    // decided the major types match.
                    return CompareArgumentEncoding(argumentA, argumentB);

                case CborMajorType.ByteString:
                case CborMajorType.TextString:
                    return CompareContentKeys(contentA, contentB);

                default:
                    throw new CborException(
                        $"CBOR major type {majorTypeA} is not supported as a deterministic map key.");
            }
        }

        /// <summary>
        /// Orders the two string major types (2 and 3), which share one rule: the encoded key is
        /// header || content and the header encodes the content length, so a shorter encoded key
        /// always sorts before a longer one and comparing headers reduces to comparing lengths.
        /// </summary>
        private static int CompareContentKeys(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            int headerComparison = CompareArgumentEncoding((ulong)a.Length, (ulong)b.Length);

            if (headerComparison != 0)
            {
                return headerComparison;
            }

            return a.SequenceCompareTo(b);
        }

        public static int CompareIntKeys(int a, int b)
        {
            // A thin wrapper over CompareIntegerKeys, converting each int to the (major-type,
            // argument) representation CBOR itself uses. Kept as the int-typed public entry point so
            // existing callers (ObjectConverter's member sort) are untouched; see CompareIntegerKeys
            // for a key space CBOR allows but int cannot represent (down to -2^64).
            bool negativeA = a < 0;
            bool negativeB = b < 0;
            ulong argumentA = negativeA ? (ulong)(-1L - a) : (ulong)a;
            ulong argumentB = negativeB ? (ulong)(-1L - b) : (ulong)b;
            return CompareIntegerKeys(negativeA, argumentA, negativeB, argumentB);
        }

        /// <summary>
        /// Compares two CBOR integer keys by their wire representation: a major type (0 for
        /// non-negative, 1 for negative) plus a <see cref="ulong"/> argument. For major type 0 the
        /// argument is the value itself; for major type 1 the argument is <c>-1 - value</c>, per RFC
        /// 8949 -- which is why this takes an argument rather than a signed value: major type 1's
        /// argument reaches <see cref="ulong.MaxValue"/>, representing values down to -2^64, a range no
        /// signed 64-bit CLR integer (<see cref="long"/> bottoms out at -2^63) can hold. Any caller that
        /// already has a CLR integer narrower than that full range -- <see cref="CompareIntKeys"/>
        /// included -- converts to this shape first.
        /// </summary>
        /// <param name="negativeA">Whether key A is CBOR major type 1 (a negative value).</param>
        /// <param name="argumentA">Key A's CBOR argument, as defined above.</param>
        /// <param name="negativeB">Whether key B is CBOR major type 1 (a negative value).</param>
        /// <param name="argumentB">Key B's CBOR argument, as defined above.</param>
        public static int CompareIntegerKeys(bool negativeA, ulong argumentA, bool negativeB, ulong argumentB)
        {
            // Major type 1's leading byte range (0x20-0x3B) is entirely above major type 0's
            // (0x00-0x1B), so every negative key sorts after every non-negative one regardless of
            // argument.
            if (negativeA != negativeB)
            {
                return negativeA ? 1 : -1;
            }

            return CompareArgumentEncoding(argumentA, argumentB);
        }

        /// <summary>
        /// Compares two arguments by the encoded form the shortest-form ladder gives them: first by
        /// how many bytes they occupy, then by value.
        /// </summary>
        /// <remarks>
        /// The tiering is load-bearing for <see cref="CompareTextKeys"/>: there, tier (header length)
        /// and tiebreaker (name content) are different data, so a shorter-tier key can and does sort
        /// before a longer-tier one whose content would otherwise compare smaller. For
        /// <see cref="CompareIntKeys"/> it is redundant but harmless: <see cref="ArgumentEncodedSize"/>
        /// partitions <see cref="ulong"/> into contiguous, strictly increasing ranges, so a lower tier
        /// always holds numerically smaller values, which makes this method identical to plain
        /// <c>a.CompareTo(b)</c> for every <see cref="ulong"/> pair. It is kept anyway so both call
        /// sites share one implementation of the rule.
        /// </remarks>
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
