using System;
using System.Runtime.InteropServices;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes RFC 8746 typed arrays (tags 64-87) for the numeric element types, falling back
    /// to the ordinary per-element array encoding when no typed array tag is present.
    /// </summary>
    public class TypedArrayConverter<TI> : ArrayConverter<TI> where TI : unmanaged
    {
        private readonly CborOptions _options;
        private readonly TypedArrayTagInfo _tagInfo;

        public TypedArrayConverter(CborOptions options)
            : base(options)
        {
            _options = options;

            if (!TypedArrayTags.TryGetByElementType(typeof(TI), out _tagInfo))
            {
                throw new CborException($"{typeof(TI)} is not a typed array element type.");
            }
        }

        public override TI[]? Read(ref CborReader reader)
        {
            // The tag must be read before anything else. Every other CborReader entry point —
            // ReadNull included — begins with SkipSemanticTag(), which consumes the tag and discards
            // the number, so a null check here would destroy the tag this converter needs.
            // TryReadSemanticTag consumes nothing when the next item is not a tag, so the null case
            // is handled correctly by base.Read below.
            if (reader.TryReadSemanticTag(out ulong tag) && TypedArrayTags.IsTypedArrayTag(tag))
            {
                return ReadTypedArray(ref reader, tag);
            }

            // Either there was no tag, or it was a tag this converter does not recognise, which CBOR
            // says to ignore. Both cases are an ordinary array — or a null, which base.Read handles.
            return base.Read(ref reader);
        }

        public override void Write(ref CborWriter writer, TI[]? value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (_options.TypedArrayMode != TypedArrayMode.LittleEndian)
            {
                base.Write(ref writer, value, lengthMode);
                return;
            }

            writer.WriteSemanticTag(_tagInfo.LittleEndianTag);

            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value.AsSpan());

            if (BitConverter.IsLittleEndian || _tagInfo.ElementSize == 1)
            {
                writer.WriteByteString(bytes);
                return;
            }

            // Big-endian host writing a little-endian tag: swap into a copy so the caller's array is
            // left alone.
            TI[] swapped = (TI[])value.Clone();
            ReverseElements(swapped);
            writer.WriteByteString(MemoryMarshal.AsBytes(swapped.AsSpan()));
        }

        private TI[] ReadTypedArray(ref CborReader reader, ulong tag)
        {
            bool bigEndian;

            if (tag == _tagInfo.LittleEndianTag)
            {
                bigEndian = false;
            }
            else if (tag == _tagInfo.BigEndianTag)
            {
                bigEndian = true;
            }
            else
            {
                throw new CborException(
                    $"Cannot read a typed array tagged {tag} ({TypedArrayTags.DescribeTag(tag)}) into {typeof(TI[])}.");
            }

            // ReadByteString always returns a contiguous span, copying out of a fragmented sequence
            // when it has to, so the cast below is always valid.
            ReadOnlySpan<byte> bytes = reader.ReadByteString();

            if (bytes.Length % _tagInfo.ElementSize != 0)
            {
                throw new CborException(
                    $"Typed array payload of {bytes.Length} bytes is not a multiple of the {_tagInfo.ElementSize} byte element size.");
            }

            TI[] result = new TI[bytes.Length / _tagInfo.ElementSize];
            MemoryMarshal.Cast<byte, TI>(bytes).CopyTo(result);

            if (bigEndian != !BitConverter.IsLittleEndian && _tagInfo.ElementSize > 1)
            {
                ReverseElements(result);
            }

            return result;
        }

        private void ReverseElements(TI[] values)
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());

            for (int offset = 0; offset < bytes.Length; offset += _tagInfo.ElementSize)
            {
                bytes.Slice(offset, _tagInfo.ElementSize).Reverse();
            }
        }
    }
}
