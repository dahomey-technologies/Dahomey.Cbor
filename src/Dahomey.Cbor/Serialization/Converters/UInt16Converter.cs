namespace Dahomey.Cbor.Serialization.Converters
{
    public class UInt16Converter : ICborConverter<ushort>
    {
        public ushort Read(ref CborReader reader)
        {
            return reader.ReadUInt16();
        }

        public void Write(ref CborWriter writer, ushort value)
        {
            writer.WriteUInt16(value);
        }
    }
}
