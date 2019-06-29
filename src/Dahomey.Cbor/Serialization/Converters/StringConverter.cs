namespace Dahomey.Cbor.Serialization.Converters
{
    public class StringConverter : ICborConverter<string>
    {
        public string Read(ref CborReader reader)
        {
            return reader.ReadString();
        }

        public void Write(ref CborWriter writer, string value)
        {
            writer.WriteString(value);
        }
    }
}
