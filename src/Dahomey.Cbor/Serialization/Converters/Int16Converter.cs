namespace Dahomey.Cbor.Serialization.Converters
{
    public class Int16Converter : CborConverterBase<short>
    {
        public override short Read(ref CborReader reader)
        {
            return reader.ReadInt16();
        }

        public override void Write(ref CborWriter writer, short value)
        {
            writer.WriteInt16(value);
        }
    }
}
