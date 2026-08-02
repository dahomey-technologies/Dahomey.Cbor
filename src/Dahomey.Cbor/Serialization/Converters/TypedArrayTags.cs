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
    /// </remarks>
    internal static class TypedArrayTags
    {
        public const ulong MinTag = 64;
        public const ulong MaxTag = 87;

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

        public static bool IsTypedArrayTag(ulong tag) => tag >= MinTag && tag <= MaxTag;

        public static bool TryGetByElementType(Type elementType, out TypedArrayTagInfo info)
            => _byElementType.TryGetValue(elementType, out info);

        public static string DescribeTag(ulong tag)
        {
            switch (tag)
            {
                case 64: return "uint8";
                case 65: return "uint16 big endian";
                case 66: return "uint32 big endian";
                case 67: return "uint64 big endian";
                case 68: return "uint8 clamped";
                case 69: return "uint16 little endian";
                case 70: return "uint32 little endian";
                case 71: return "uint64 little endian";
                case 72: return "sint8";
                case 73: return "sint16 big endian";
                case 74: return "sint32 big endian";
                case 75: return "sint64 big endian";
                case 76: return "reserved";
                case 77: return "sint16 little endian";
                case 78: return "sint32 little endian";
                case 79: return "sint64 little endian";
                case 80: return "binary16 big endian";
                case 81: return "binary32 big endian";
                case 82: return "binary64 big endian";
                case 83: return "binary128 big endian";
                case 84: return "binary16 little endian";
                case 85: return "binary32 little endian";
                case 86: return "binary64 little endian";
                case 87: return "binary128 little endian";
                default: return $"tag {tag}";
            }
        }
    }
}
