namespace Dahomey.Cbor.Serialization.Converters
{
    public class BooleanConverter : ICborConverter<bool>
    {
        public bool Read(ref CborReader reader)
        {
            return reader.ReadBoolean();
        }

        public void Write(ref CborWriter writer, bool value)
        {
            writer.WriteBoolean(value);
        }
    }
}
