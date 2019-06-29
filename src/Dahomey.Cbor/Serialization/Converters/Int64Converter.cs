namespace Dahomey.Cbor.Serialization.Converters
{
    public class Int64Converter : ICborConverter<long>
    {
        public long Read(ref CborReader reader)
        {
            return reader.ReadInt64();
        }

        public void Write(ref CborWriter writer, long value)
        {
            writer.WriteInt64(value);
        }
    }
}
