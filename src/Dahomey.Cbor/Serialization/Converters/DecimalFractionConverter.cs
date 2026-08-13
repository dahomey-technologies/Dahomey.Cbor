namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes <see cref="CborDecimalFraction"/> as an RFC 8949 §3.4.4 decimal fraction,
    /// tag 4.
    /// </summary>
    public class DecimalFractionConverter : CborConverterBase<CborDecimalFraction>
    {
        public override CborDecimalFraction Read(ref CborReader reader)
        {
            return reader.ReadDecimalFraction();
        }

        public override void Write(ref CborWriter writer, CborDecimalFraction value)
        {
            writer.WriteDecimalFraction(value);
        }
    }
}
