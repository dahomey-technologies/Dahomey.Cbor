using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="Guid"/> as an RFC 9562 §4 binary UUID, tag 37.
    /// </summary>
    /// <remarks>
    /// The byte order is the RFC's, not the CLR's. <see cref="Guid.ToByteArray()"/> emits the first
    /// three fields little-endian -- the layout Microsoft's GUID has always had -- while RFC 9562 lays
    /// a UUID out big-endian throughout, and that is what every other implementation of tag 37 reads.
    /// So the three fields are reversed on the way out and again on the way in; the trailing eight
    /// bytes are a byte array in both and are left alone.
    /// <para>
    /// Internal on purpose: nothing names it, since the registry and the generated contexts resolve it
    /// by type, and a public <c>GuidConverter</c> would be ambiguous (CS0104) in every project that
    /// copied a converter of that name into its own code.
    /// </para>
    /// <para>
    /// A byte string must carry tag 37. Without it the sixteen bytes could be this layout or the CLR's,
    /// which differ only in order and so decode to a different valid <see cref="Guid"/> without error;
    /// <see cref="CborOptions.UntaggedGuidFormat"/> is how a caller states the layout of old data.
    /// </para>
    /// </remarks>
    internal class GuidConverter : CborConverterBase<Guid>
    {
        private readonly CborOptions _options;

        public GuidConverter(CborOptions options)
        {
            _options = options;
        }

        /// <summary>RFC 9562's tag for a binary UUID.</summary>
        internal const ulong BinaryUuidTag = 37;

        private const int UuidLength = 16;

        /// <summary>The canonical 8-4-4-4-12 rendering, which is what a text string carries.</summary>
        private const int CanonicalTextLength = 36;

        public override Guid Read(ref CborReader reader)
        {
            bool tagged = reader.TryReadSemanticTag(out ulong tag);

            if (tagged && tag != BinaryUuidTag)
            {
                throw reader.BuildException(
                    $"Invalid semantic tag {tag} for a UUID, expected {BinaryUuidTag}");
            }

            if (reader.IsSemanticTag())
            {
                throw reader.BuildException("Invalid UUID: more than one semantic tag");
            }

            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.ByteString:
                {
                    bool clrOrder = false;

                    if (!tagged)
                    {
                        if (_options.UntaggedGuidFormat != UntaggedGuidFormat.ClrByteOrder)
                        {
                            throw reader.BuildException(
                                "A UUID byte string without tag 37 is refused: its byte order cannot be told "
                                + "from RFC 9562's, and a wrong guess yields a different UUID with no error. "
                                + "Data written as Guid.ToByteArray() bytes can be read by setting "
                                + "CborOptions.UntaggedGuidFormat to UntaggedGuidFormat.ClrByteOrder");
                        }

                        clrOrder = true;
                    }

                    ReadOnlySpan<byte> bytes = reader.ReadByteString();

                    if (bytes.Length != UuidLength)
                    {
                        throw reader.BuildException(
                            $"Invalid UUID length {bytes.Length}, expected {UuidLength} bytes");
                    }

                    byte[] guidBytes = bytes.ToArray();

                    if (!clrOrder)
                    {
                        SwapFieldOrder(guidBytes);
                    }

                    return new Guid(guidBytes);
                }

                // Not a form this writes, and not what tag 37 carries, but the shape a peer emitting
                // the textual rendering of a UUID would send. Reading it costs nothing and refusing it
                // would buy nothing.
                case CborDataItemType.String:
                {
                    if (tagged)
                    {
                        throw reader.BuildException("Invalid UUID: tag 37 is defined over a byte string");
                    }

                    string? text = reader.ReadString();

                    if (text is null
                        || text.Length != CanonicalTextLength
                        || !Guid.TryParseExact(text, "D", out Guid parsed))
                    {
                        throw reader.BuildException($"Invalid UUID format {text}");
                    }

                    return parsed;
                }

                default:
                    throw reader.BuildException("Invalid UUID format");
            }
        }

        public override void Write(ref CborWriter writer, Guid value)
        {
            byte[] bytes = value.ToByteArray();
            SwapFieldOrder(bytes);

            writer.WriteSemanticTag(BinaryUuidTag);
            writer.WriteByteString(bytes);
        }

        /// <summary>
        /// Converts between the CLR's field order and RFC 9562's, in place. The operation is its own
        /// inverse, so one method serves both directions.
        /// </summary>
        /// <remarks>
        /// Done by hand rather than through <c>Guid.ToByteArray(bool bigEndian)</c>, which arrived in
        /// .NET 8 and so does not exist on netstandard2.0. Spelling it out keeps every target on
        /// identical arithmetic instead of forking the behaviour by framework.
        /// </remarks>
        private static void SwapFieldOrder(byte[] bytes)
        {
            Swap(bytes, 0, 3);
            Swap(bytes, 1, 2);
            Swap(bytes, 4, 5);
            Swap(bytes, 6, 7);
        }

        private static void Swap(byte[] bytes, int left, int right)
        {
            byte swap = bytes[left];
            bytes[left] = bytes[right];
            bytes[right] = swap;
        }
    }
}
