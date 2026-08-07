using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// One row of the RFC 8746 typed array tag table.
    /// </summary>
    internal readonly struct TypedArrayTagInfo
    {
        public readonly int ElementSize;
        public readonly ulong LittleEndianTag;
        public readonly ulong BigEndianTag;

        public TypedArrayTagInfo(int elementSize, ulong littleEndianTag, ulong bigEndianTag)
        {
            ElementSize = elementSize;
            LittleEndianTag = littleEndianTag;
            BigEndianTag = bigEndianTag;
        }
    }

    /// <summary>
    /// The RFC 8746 typed array tag table, keyed by .NET element type.
    /// </summary>
    /// <remarks>
    /// byte[] is deliberately absent. Tag 64 exists for it, but a plain major-type-2 byte string is
    /// both shorter and idiomatic, and is what <see cref="ByteArrayConverter"/> already writes.
    /// Tag 76 is reserved by RFC 8746; tags 83 and 87 are IEEE 754 binary128, which has no .NET type.
    /// <para>
    /// MATCHED PAIR: the set of keys below is duplicated as <c>TypeCollector.IsTypedArrayElementType</c>
    /// in <c>src/Dahomey.Cbor.Generator/TypeCollector.cs</c>, which decides when the source-generated
    /// path emits <see cref="TypedArrayConverter{TI}"/>. It cannot be shared — the generator is an
    /// analyzer assembly and must not reference this library — so adding or removing an element type
    /// here requires the same edit there. <c>GeneratedTypedArrayTests</c> asserts the two agree.
    /// </para>
    /// </remarks>
    internal static class TypedArrayTags
    {
        internal const ulong MinTag = 64;
        internal const ulong MaxTag = 87;

        private static readonly Dictionary<Type, TypedArrayTagInfo> _byElementType =
            new Dictionary<Type, TypedArrayTagInfo>
            {
                [typeof(sbyte)] = new TypedArrayTagInfo(1, 72, 72),
                [typeof(ushort)] = new TypedArrayTagInfo(2, 69, 65),
                [typeof(short)] = new TypedArrayTagInfo(2, 77, 73),
                [typeof(uint)] = new TypedArrayTagInfo(4, 70, 66),
                [typeof(int)] = new TypedArrayTagInfo(4, 78, 74),
                [typeof(ulong)] = new TypedArrayTagInfo(8, 71, 67),
                [typeof(long)] = new TypedArrayTagInfo(8, 79, 75),
                [typeof(Half)] = new TypedArrayTagInfo(2, 84, 80),
                [typeof(float)] = new TypedArrayTagInfo(4, 85, 81),
                [typeof(double)] = new TypedArrayTagInfo(8, 86, 82),
            };

        /// <summary>
        /// RFC 8746 §2, indexed by tag - <see cref="MinTag"/>.
        /// </summary>
        private static readonly string[] _descriptionsByTag =
        {
            "uint8", "uint16 big endian", "uint32 big endian", "uint64 big endian",
            "uint8 clamped", "uint16 little endian", "uint32 little endian", "uint64 little endian",
            "sint8", "sint16 big endian", "sint32 big endian", "sint64 big endian",
            "reserved", "sint16 little endian", "sint32 little endian", "sint64 little endian",
            "binary16 big endian", "binary32 big endian", "binary64 big endian", "binary128 big endian",
            "binary16 little endian", "binary32 little endian", "binary64 little endian", "binary128 little endian",
        };

        internal static bool IsTypedArrayTag(ulong tag) => tag >= MinTag && tag <= MaxTag;

        internal static bool TryGetByElementType(Type elementType, out TypedArrayTagInfo info)
            => _byElementType.TryGetValue(elementType, out info);

        internal static string DescribeTag(ulong tag)
            => IsTypedArrayTag(tag) ? _descriptionsByTag[tag - MinTag] : $"tag {tag}";
    }
}
