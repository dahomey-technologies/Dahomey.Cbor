namespace Dahomey.Cbor.Serialization.Converters
{
    public class DoubleConverter : CborConverterBase<double>
    {
        public override double Read(ref CborReader reader)
        {
            return reader.ReadDouble();
        }

        public override void Write(ref CborWriter writer, double value)
        {
            writer.WriteDouble(value);
        }
    }
}
