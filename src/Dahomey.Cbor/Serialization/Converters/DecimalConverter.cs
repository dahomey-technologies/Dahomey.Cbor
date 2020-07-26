namespace Dahomey.Cbor.Serialization.Converters
{
    public class DecimalConverter : CborConverterBase<decimal>
    {
        public override decimal Read(ref CborReader reader)
        {
            return reader.ReadDecimal();
        }

        public override void Write(ref CborWriter writer, decimal value)
        {
            writer.WriteDecimal(value);
        }
    }
}
