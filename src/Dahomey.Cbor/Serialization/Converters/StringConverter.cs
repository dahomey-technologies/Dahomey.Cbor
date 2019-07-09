namespace Dahomey.Cbor.Serialization.Converters
{
    public class StringConverter : CborConverterBase<string>
    {
        public override string Read(ref CborReader reader)
        {
            return reader.ReadString();
        }

        public override void Write(ref CborWriter writer, string value)
        {
            writer.WriteString(value);
        }
    }
}
