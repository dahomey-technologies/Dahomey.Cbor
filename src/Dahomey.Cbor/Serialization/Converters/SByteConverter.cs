namespace Dahomey.Cbor.Serialization.Converters
{
    public class SByteConverter : ICborConverter<sbyte>
    {
        public sbyte Read(ref CborReader reader)
        {
            return reader.ReadSByte();
        }

        public void Write(ref CborWriter writer, sbyte value)
        {
            writer.WriteSByte(value);
        }
    }
}
