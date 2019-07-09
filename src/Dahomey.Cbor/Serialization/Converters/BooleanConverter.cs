namespace Dahomey.Cbor.Serialization.Converters
{
    public class BooleanConverter : CborConverterBase<bool>
    {
        public override bool Read(ref CborReader reader)
        {
            return reader.ReadBoolean();
        }

        public override void Write(ref CborWriter writer, bool value)
        {
            writer.WriteBoolean(value);
        }
    }
}
