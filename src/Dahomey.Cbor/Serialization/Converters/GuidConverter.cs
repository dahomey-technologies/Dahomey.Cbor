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
    /// </remarks>
    public class GuidConverter : CborConverterBase<Guid>
    {
        /// <summary>RFC 9562's tag for a binary UUID.</summary>
        public const ulong BinaryUuidTag = 37;

        private const int UuidLength = 16;

        /// <summary>The canonical 8-4-4-4-12 rendering, which is what a text string carries.</summary>
        private const int CanonicalTextLength = 36;

        public override Guid Read(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.ByteString:
                {
                    ReadOnlySpan<byte> bytes = reader.ReadByteString();

                    if (bytes.Length != UuidLength)
                    {
                        throw reader.BuildException(
                            $"Invalid UUID length {bytes.Length}, expected {UuidLength} bytes");
                    }

                    byte[] clrOrder = bytes.ToArray();
                    SwapFieldOrder(clrOrder);
                    return new Guid(clrOrder);
                }

                // Not a form this writes, and not what tag 37 carries, but the shape a peer emitting
                // the textual rendering of a UUID would send. Reading it costs nothing and refusing it
                // would buy nothing.
                case CborDataItemType.String:
                {
                    string text = reader.ReadString();

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
