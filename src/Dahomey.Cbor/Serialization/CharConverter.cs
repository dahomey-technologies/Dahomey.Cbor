namespace Dahomey.Cbor.Serialization.Converters
{
    public class CharConverter : CborConverterBase<char>
    {
        public override char Read(ref CborReader reader)
        {
            return reader.ReadChar();
        }

        public override void Write(ref CborWriter writer, char value)
        {
            writer.WriteChar(value);
        }
    }
}
