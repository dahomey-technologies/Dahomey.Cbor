using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Puts the entries of a map in RFC 8949 section 4.2.1 key order, for the two converters whose key
    /// set is only known at write time: <see cref="AbstractDictionaryConverter{TC, TK, TV}"/> and the
    /// <see cref="CborObject"/> half of <see cref="CborValueConverter"/>. Fixed object members are not
    /// routed through here; <see cref="ObjectConverter{T}"/> sorts its own member list, which is known
    /// once per type rather than once per write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Section 4.2.1 orders keys by the bytes of their encoded form, so that is what this sorts on:
    /// every key is encoded once, through the same converter that will write it into the document, and
    /// the sort compares those bytes directly. Nothing here knows what any particular key type encodes
    /// to, which is the point — a sort key derived from the CLR type would be a second opinion about
    /// what the converter emits, and a second opinion can be wrong. It was: an enum whose value has two
    /// names sorted under the first name and wrote the last one, a user-registered converter for a
    /// built-in type was ignored entirely, and key types with no case in the switch threw even though
    /// their converter could write them perfectly well.
    /// </para>
    /// <para>
    /// Decorate-sort-undecorate, still: n encodings up front rather than the O(n log n) a comparison
    /// that encoded both sides would cost. The keys are encoded a second time when the map is written,
    /// which is the price of not holding an intermediate representation of the whole map.
    /// </para>
    /// <para>
    /// Encoding happens before the sort starts, so a converter that throws does so outside the
    /// comparison. That matters: <see cref="Array.Sort{TKey, TValue}(TKey[], TValue[], IComparer{TKey})"/>
    /// wraps anything the comparison throws in an <see cref="InvalidOperationException"/>, and the way
    /// to keep a <see cref="CborException"/> intact is not to throw it from there.
    /// </para>
    /// </remarks>
    internal static class DeterministicKeyOrder
    {
        /// <summary>
        /// Returns the entries in deterministic key order, encoding each key exactly once with
        /// <paramref name="keyConverter" />.
        /// </summary>
        /// <param name="maxDepth">
        /// Depth bound for the scratch writer. A key is normally a scalar, but nothing forbids a
        /// composite one, and the bound should be the caller's rather than the default.
        /// </param>
        public static KeyValuePair<TK, TV>[] Sort<TK, TV>(
            ICollection<KeyValuePair<TK, TV>> entries,
            ICborConverter<TK> keyConverter,
            int maxDepth)
        {
            KeyValuePair<TK, TV>[] sorted = new KeyValuePair<TK, TV>[entries.Count];
            entries.CopyTo(sorted, 0);

            if (sorted.Length < 2)
            {
                return sorted;
            }

            byte[] encoded;
            int[] offsets = new int[sorted.Length + 1];

            // One buffer for every key, with the boundaries recorded as we go, so the whole set costs
            // a single growing allocation rather than one per key.
            using (ByteBufferWriter keyBuffer = new ByteBufferWriter())
            {
                CborWriter keyWriter = new CborWriter(keyBuffer, maxDepth, deterministic: true);

                for (int i = 0; i < sorted.Length; i++)
                {
                    keyConverter.Write(ref keyWriter, sorted[i].Key);
                    offsets[i + 1] = keyBuffer.WrittenSpan.Length;
                }

                encoded = keyBuffer.WrittenSpan.ToArray();
            }

            int[] order = new int[sorted.Length];

            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // Bytewise over the encoded keys, which is section 4.2.1 read literally. Length is not
            // compared separately: a shorter key already sorts first because the length is part of the
            // header byte the comparison starts at.
            Array.Sort(order, (x, y) => Compare(encoded, offsets, x, y));

            KeyValuePair<TK, TV>[] result = new KeyValuePair<TK, TV>[sorted.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = sorted[order[i]];
            }

            return result;
        }

        private static int Compare(byte[] encoded, int[] offsets, int x, int y)
        {
            ReadOnlySpan<byte> left = encoded.AsSpan(offsets[x], offsets[x + 1] - offsets[x]);
            ReadOnlySpan<byte> right = encoded.AsSpan(offsets[y], offsets[y + 1] - offsets[y]);

            return left.SequenceCompareTo(right);
        }
    }
}
