using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;
using System.Text;

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
    /// Decorate-sort-undecorate: every key is reduced to a <see cref="SortKey"/> once, up front, and the
    /// sort then compares those. Doing the reduction inside the comparison instead would re-encode both
    /// sides of every comparison, which is O(n log n) encodings for a result that only ever needs n.
    /// </para>
    /// <para>
    /// A key kind that has no deterministic order is rejected during decoration, before the sort starts.
    /// That is also why neither caller unwraps an exception: <see cref="List{T}.Sort(Comparison{T})"/>
    /// and <see cref="Array.Sort{TKey, TValue}(TKey[], TValue[], IComparer{TKey})"/> wrap anything the
    /// comparison throws in an <see cref="InvalidOperationException"/>, and the way to keep a
    /// <see cref="CborException"/> intact is to not throw it from the comparison in the first place.
    /// </para>
    /// </remarks>
    internal static class DeterministicKeyOrder
    {
        /// <summary>
        /// A map key reduced to what its encoded form is ordered by: its major type, plus the argument
        /// (integer keys) or the raw payload (string keys) that distinguishes keys of that major type.
        /// </summary>
        internal readonly struct SortKey
        {
            public readonly CborMajorType MajorType;
            public readonly ulong Argument;
            public readonly byte[] Content;

            private SortKey(CborMajorType majorType, ulong argument, byte[] content)
            {
                MajorType = majorType;
                Argument = argument;
                Content = content;
            }

            public static SortKey Integer(bool negative, ulong argument)
            {
                return new SortKey(
                    negative ? CborMajorType.NegativeInteger : CborMajorType.PositiveInteger,
                    argument,
                    Array.Empty<byte>());
            }

            public static SortKey Text(byte[] utf8Content)
            {
                return new SortKey(CborMajorType.TextString, 0, utf8Content);
            }

            public static SortKey Bytes(byte[] content)
            {
                return new SortKey(CborMajorType.ByteString, 0, content);
            }
        }

        private sealed class SortKeyComparer : IComparer<SortKey>
        {
            public static readonly SortKeyComparer Instance = new SortKeyComparer();

            public int Compare(SortKey x, SortKey y)
            {
                return CborKeyComparer.CompareKeys(
                    x.MajorType, x.Argument, x.Content,
                    y.MajorType, y.Argument, y.Content);
            }
        }

        /// <summary>
        /// Returns the entries of <paramref name="entries"/> in deterministic key order, decorating each
        /// key exactly once with <paramref name="decorate"/>.
        /// </summary>
        public static KeyValuePair<TK, TV>[] Sort<TK, TV>(
            ICollection<KeyValuePair<TK, TV>> entries,
            Func<TK, SortKey> decorate)
        {
            KeyValuePair<TK, TV>[] sorted = new KeyValuePair<TK, TV>[entries.Count];
            entries.CopyTo(sorted, 0);

            SortKey[] sortKeys = new SortKey[sorted.Length];

            for (int i = 0; i < sorted.Length; i++)
            {
                sortKeys[i] = decorate(sorted[i].Key);
            }

            // The paired overload permutes both arrays together, so the entries end up ordered by their
            // decorated keys without the comparison ever touching an entry. Stability is not needed:
            // map keys are unique, and distinct keys have distinct encodings.
            Array.Sort(sortKeys, sorted, SortKeyComparer.Instance);

            return sorted;
        }

        /// <summary>
        /// Decorates a CLR dictionary key. Every kind handled here is one the corresponding key
        /// converter can already write, and each is decorated as the major type that converter emits --
        /// so the order computed here is the order of the bytes that actually get written.
        /// </summary>
        public static SortKey ForDictionaryKey(object key, CborOptions options)
        {
            switch (key)
            {
                case string text:
                    return SortKey.Text(Encoding.UTF8.GetBytes(text));

                // CharConverter writes a char as a one-character text string (CborWriter.WriteChar
                // UTF-8-encodes it), not as an integer, so it is ordered as text.
                case char character:
                    return SortKey.Text(Encoding.UTF8.GetBytes(character.ToString()));

                case byte[] bytes:
                    return SortKey.Bytes(bytes);

                case ReadOnlyMemory<byte> memory:
                    return SortKey.Bytes(memory.ToArray());

                case Enum enumKey:
                    return ForEnumKey(enumKey, options);
            }

            if (TryGetIntegerArgument(key, out bool negative, out ulong argument))
            {
                return SortKey.Integer(negative, argument);
            }

            throw new CborException(
                $"Deterministic encoding does not define an order for {key.GetType()} map keys; "
                + "supported key types are string, char, byte string, any integral type and enums.");
        }

        /// <summary>
        /// Decorates a <see cref="CborObject"/> key, which carries its wire type as
        /// <see cref="CborValue.Type"/> rather than as a distinct CLR type per kind.
        /// </summary>
        public static SortKey ForCborValueKey(CborValue key)
        {
            switch (key.Type)
            {
                case CborValueType.String:
                    return SortKey.Text(Encoding.UTF8.GetBytes(key.Value<string>()));

                // Byte strings are legal map keys and are what a map read off the wire may well be keyed
                // by; CborValueConverter writes them with WriteByteString, i.e. major type 2.
                case CborValueType.ByteString:
                    return SortKey.Bytes(key.Value<ReadOnlyMemory<byte>>().ToArray());

                // Read at full width -- Value<ulong>()/Value<long>(), never Value<int>() -- because a
                // key outside int range would otherwise wrap silently and compare as some other key.
                case CborValueType.Positive:
                    FromUnsigned(key.Value<ulong>(), out bool positiveNegative, out ulong positiveArgument);
                    return SortKey.Integer(positiveNegative, positiveArgument);

                case CborValueType.Negative:
                    FromSigned(key.Value<long>(), out bool negativeNegative, out ulong negativeArgument);
                    return SortKey.Integer(negativeNegative, negativeArgument);

                default:
                    throw new CborException(
                        $"Deterministic encoding does not define an order for {key.Type} CborObject keys; "
                        + "supported key types are String, ByteString, Positive and Negative.");
            }
        }

        /// <summary>
        /// Converts a boxed CLR integer of any width and signedness to the (major type, argument)
        /// representation <see cref="CborKeyComparer.CompareIntegerKeys"/> takes.
        /// </summary>
        /// <remarks>
        /// The narrow types all widen losslessly, so they need no case of their own beyond naming them:
        /// a boxed <see cref="short"/> does not match <c>case int</c>, because unboxing is exact-type.
        /// That is the same reason <see cref="Enum"/> needs its own branch above.
        /// </remarks>
        internal static bool TryGetIntegerArgument(object key, out bool negative, out ulong argument)
        {
            switch (key)
            {
                case sbyte value:
                    return FromSigned(value, out negative, out argument);
                case short value:
                    return FromSigned(value, out negative, out argument);
                case int value:
                    return FromSigned(value, out negative, out argument);
                case long value:
                    return FromSigned(value, out negative, out argument);
                case byte value:
                    return FromUnsigned(value, out negative, out argument);
                case ushort value:
                    return FromUnsigned(value, out negative, out argument);
                case uint value:
                    return FromUnsigned(value, out negative, out argument);
                case ulong value:
                    return FromUnsigned(value, out negative, out argument);
            }

            negative = false;
            argument = 0;
            return false;
        }

        private static bool FromSigned(long value, out bool negative, out ulong argument)
        {
            negative = value < 0;
            // Computed in long, where -1 - long.MinValue is long.MaxValue and stays in range; the
            // negative major type's argument is -1 - value per RFC 8949.
            argument = negative ? (ulong)(-1L - value) : (ulong)value;
            return true;
        }

        private static bool FromUnsigned(ulong value, out bool negative, out ulong argument)
        {
            negative = false;
            argument = value;
            return true;
        }

        /// <summary>
        /// Decorates an enum key the way <see cref="EnumConverter{T}"/> writes one.
        /// </summary>
        /// <remarks>
        /// Two branches, matching that converter: with <see cref="ValueFormat.WriteToString"/> a named
        /// value is written as its name, and everything else is written through
        /// <c>Unsafe.As&lt;T, int&gt;</c> -- a 32-bit reinterpretation, so an enum whose underlying type
        /// is wider than 32 bits is written truncated. The order has to follow the bytes that are
        /// actually emitted, so the truncation is reproduced here rather than corrected.
        /// </remarks>
        private static SortKey ForEnumKey(Enum key, CborOptions options)
        {
            if (options.EnumFormat == ValueFormat.WriteToString)
            {
                string? name = Enum.GetName(key.GetType(), key);

                if (name != null)
                {
                    // EnumConverter holds its names as ASCII, and an enum member name is an identifier,
                    // so ASCII and UTF-8 agree byte for byte here.
                    return SortKey.Text(Encoding.ASCII.GetBytes(name));
                }

                // An unnamed value falls back to the integer form, exactly as EnumConverter.WriteString
                // does.
            }

            Type underlyingType = Enum.GetUnderlyingType(key.GetType());
            IConvertible convertible = key;

            // Only UInt64 can hold a value that ToInt64 would reject; every narrower unsigned type
            // widens into long without loss.
            ulong bits = Type.GetTypeCode(underlyingType) == TypeCode.UInt64
                ? convertible.ToUInt64(null)
                : unchecked((ulong)convertible.ToInt64(null));

            FromSigned(unchecked((int)(uint)bits), out bool negative, out ulong argument);
            return SortKey.Integer(negative, argument);
        }
    }
}
