namespace Dahomey.Cbor.Serialization.Converters
{
    public class ByteArrayConverter : CborConverterBase<byte[]>
    {
        public override byte[] Read(ref CborReader reader)
        {
            return reader.ReadByteString().ToArray();
        }

        public override void Write(ref CborWriter writer, byte[] value)
        {
            writer.WriteByteString(value);
        }
    }
}
