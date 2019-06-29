namespace Dahomey.Cbor.Serialization.Converters
{
    public class UInt32Converter : ICborConverter<uint>
    {
        public uint Read(ref CborReader reader)
        {
            return reader.ReadUInt32();
        }

        public void Write(ref CborWriter writer, uint value)
        {
            writer.WriteUInt32(value);
        }
    }
}
