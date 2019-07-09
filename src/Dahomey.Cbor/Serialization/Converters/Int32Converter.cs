namespace Dahomey.Cbor.Serialization.Converters
{
    public class Int32Converter : CborConverterBase<int>
    {
        public override int Read(ref CborReader reader)
        {
            return reader.ReadInt32();
        }

        public override void Write(ref CborWriter writer, int value)
        {
            writer.WriteInt32(value);
        }
    }
}
