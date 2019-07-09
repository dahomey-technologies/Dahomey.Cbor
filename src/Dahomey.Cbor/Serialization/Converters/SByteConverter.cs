namespace Dahomey.Cbor.Serialization.Converters
{
    public class SByteConverter : CborConverterBase<sbyte>
    {
        public override sbyte Read(ref CborReader reader)
        {
            return reader.ReadSByte();
        }

        public override void Write(ref CborWriter writer, sbyte value)
        {
            writer.WriteSByte(value);
        }
    }
}
