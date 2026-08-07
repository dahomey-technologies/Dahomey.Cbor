using System;
using System.Runtime.InteropServices;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Decodes an RFC 8746 typed array payload whose tag has already been consumed.
    /// </summary>
    /// <remarks>
    /// This is the seam that lets a collection converter reuse the array converter's decoding.
    /// <see cref="AbstractCollectionConverter{TC, TI}"/> has no <c>unmanaged</c> constraint on its
    /// element type, so it cannot name <see cref="TypedArrayConverter{TI}"/>, but it can ask the
    /// registry for the converter of <c>TI[]</c> and test for this interface.
    /// </remarks>
    internal interface ITypedArrayReader<TI>
    {
        TI[] ReadTypedArray(ref CborReader reader, ulong tag);
    }

    /// <summary>
    /// Reads and writes RFC 8746 typed arrays (tags 64-87) for the numeric element types, falling back
    /// to the ordinary per-element array encoding when no typed array tag is present.
    /// </summary>
    /// <remarks>
    /// Public for the same reason <see cref="ArrayConverter{TI}"/> is: a source-generated context names
    /// it in a registration, and that code is compiled into the consumer's assembly rather than this
    /// one, so an internal type is unreachable there. The constructor taking the host byte order stays
    /// internal -- it exists so both orders can be exercised on little-endian hardware, and it is not
    /// something a caller should be choosing.
    /// </remarks>
    public class TypedArrayConverter<TI> : ArrayConverter<TI>, ITypedArrayReader<TI> where TI : unmanaged
    {
        private readonly TypedArrayTagInfo _tagInfo;
        private readonly bool _hostIsLittleEndian;

        public TypedArrayConverter(CborOptions options)
            : this(options, BitConverter.IsLittleEndian)
        {
        }

        /// <summary>
        /// Takes the host byte order rather than reading <see cref="BitConverter.IsLittleEndian"/>, so
        /// that the byte-swapping paths can be exercised on hardware of either order.
        /// </summary>
        internal TypedArrayConverter(CborOptions options, bool hostIsLittleEndian)
            : base(options)
        {
            _hostIsLittleEndian = hostIsLittleEndian;

            if (!TypedArrayTags.TryGetByElementType(typeof(TI), out _tagInfo))
            {
                throw new CborException($"{typeof(TI)} is not a typed array element type.");
            }
        }

        public override TI[]? Read(ref CborReader reader)
        {
            if ((_options.TypedArrayMode & TypedArrayMode.Read) != 0 && reader.IsSemanticTag())
            {
                CborReaderBookmark bookmark = reader.GetBookmark();

                if (reader.TryReadSemanticTag(out ulong tag) && TypedArrayTags.IsTypedArrayTag(tag))
                {
                    return ReadTypedArray(ref reader, tag);
                }

                // Some other tag, which CBOR says to ignore. Hand it back rather than keeping it
                // consumed: base.Read skips exactly one tag, so returning it here leaves this converter
                // exactly as lenient about nesting as ArrayConverter, no more.
                reader.ReturnToBookmark(bookmark);
            }

            return base.Read(ref reader);
        }

        public override void Write(ref CborWriter writer, TI[]? value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if ((_options.TypedArrayMode & TypedArrayMode.WriteLittleEndian) == 0)
            {
                base.Write(ref writer, value, lengthMode);
                return;
            }

            writer.WriteSemanticTag(_tagInfo.LittleEndianTag);

            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(value.AsSpan());

            if (_hostIsLittleEndian || _tagInfo.ElementSize == 1)
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

        public TI[] ReadTypedArray(ref CborReader reader, ulong tag)
        {
            bool payloadIsBigEndian;

            if (tag == _tagInfo.LittleEndianTag)
            {
                payloadIsBigEndian = false;
            }
            else if (tag == _tagInfo.BigEndianTag)
            {
                payloadIsBigEndian = true;
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

            if (payloadIsBigEndian == _hostIsLittleEndian && _tagInfo.ElementSize > 1)
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
