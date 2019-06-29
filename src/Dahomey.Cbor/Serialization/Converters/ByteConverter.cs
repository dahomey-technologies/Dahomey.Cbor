namespace Dahomey.Cbor.Serialization.Converters
{
    public class ByteConverter : ICborConverter<byte>
    {
        public byte Read(ref CborReader reader)
        {
            return reader.ReadByte();
        }

        public void Write(ref CborWriter writer, byte value)
        {
            writer.WriteByte(value);
        }
    }
}
