namespace Dahomey.Cbor.Serialization.Converters
{
    public class DoubleConverter : ICborConverter<double>
    {
        public double Read(ref CborReader reader)
        {
            return reader.ReadDouble();
        }

        public void Write(ref CborWriter writer, double value)
        {
            writer.WriteDouble(value);
        }
    }
}
