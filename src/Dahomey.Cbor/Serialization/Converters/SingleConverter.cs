namespace Dahomey.Cbor.Serialization.Converters
{
    public class SingleConverter : CborConverterBase<float>
    {
        public override float Read(ref CborReader reader)
        {
            return reader.ReadSingle();
        }

        public override void Write(ref CborWriter writer, float value)
        {
            writer.WriteSingle(value);
        }
    }
}
