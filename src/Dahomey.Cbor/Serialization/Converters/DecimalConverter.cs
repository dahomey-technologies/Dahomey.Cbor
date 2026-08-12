namespace Dahomey.Cbor.Serialization.Converters
{
    public class DecimalConverter : CborConverterBase<decimal>
    {
        private readonly CborOptions _options;

        public DecimalConverter(CborOptions options)
        {
            _options = options;
        }

        public override decimal Read(ref CborReader reader)
        {
            return reader.ReadDecimal();
        }

        /// <summary>
        /// Writes the form <see cref="CborOptions.DecimalFormat"/> asks for. Reading takes both forms
        /// whatever it says, so only this half consults it.
        /// </summary>
        public override void Write(ref CborWriter writer, decimal value)
        {
            writer.WriteDecimal(value, _options.DecimalFormat);
        }
    }
}
