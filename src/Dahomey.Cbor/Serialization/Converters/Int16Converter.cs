namespace Dahomey.Cbor.Serialization.Converters
{
    public class Int16Converter : ICborConverter<short>
    {
        public short Read(ref CborReader reader)
        {
            return reader.ReadInt16();
        }

        public void Write(ref CborWriter writer, short value)
        {
            writer.WriteInt16(value);
        }
    }
}
