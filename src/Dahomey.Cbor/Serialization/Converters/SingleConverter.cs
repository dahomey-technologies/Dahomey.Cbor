namespace Dahomey.Cbor.Serialization.Converters
{
    public class SingleConverter : ICborConverter<float>
    {
        public float Read(ref CborReader reader)
        {
            return reader.ReadSingle();
        }

        public void Write(ref CborWriter writer, float value)
        {
            writer.WriteSingle(value);
        }
    }
}
