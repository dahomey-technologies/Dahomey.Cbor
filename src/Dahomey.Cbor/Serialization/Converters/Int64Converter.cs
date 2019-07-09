namespace Dahomey.Cbor.Serialization.Converters
{
    public class Int64Converter : CborConverterBase<long>
    {
        public override long Read(ref CborReader reader)
        {
            return reader.ReadInt64();
        }

        public override void Write(ref CborWriter writer, long value)
        {
            writer.WriteInt64(value);
        }
    }
}
