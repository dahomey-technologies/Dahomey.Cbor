namespace Dahomey.Cbor.Serialization.Converters
{
    public class Int32Converter : ICborConverter<int>
    {
        public int Read(ref CborReader reader)
        {
            return reader.ReadInt32();
        }

        public void Write(ref CborWriter writer, int value)
        {
            writer.WriteInt32(value);
        }
    }
}
