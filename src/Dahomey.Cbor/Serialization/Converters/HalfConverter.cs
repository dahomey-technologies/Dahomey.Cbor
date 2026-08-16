namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// <see cref="System.Half"/> as RFC 8949's binary16 — major type 7, additional value 25.
    /// </summary>
    /// <remarks>
    /// Without this, <c>Half</c> reached <c>ObjectConverterProvider</c> and was mapped like any other
    /// struct, so a member typed <c>Half</c> was written as its own internal fields —
    /// <c>BiasedExponent</c>, <c>Exponent</c>, <c>Significand</c>, <c>_value</c> — silently, and as a
    /// document no other decoder can read.
    /// <para>
    /// Both directions defer to <see cref="CborReader"/> and <see cref="CborWriter"/>, which already had
    /// the half-float primitive: the reader accepts the same shapes <c>ReadSingle</c> does — an integer,
    /// a text string, any of the float widths — so a <c>Half</c> member is exactly as tolerant of what a
    /// sender chose as a <c>float</c> member is, rather than having a policy of its own.
    /// </para>
    /// </remarks>
    public class HalfConverter : CborConverterBase<System.Half>
    {
        public override System.Half Read(ref CborReader reader)
        {
            return reader.ReadHalf();
        }

        public override void Write(ref CborWriter writer, System.Half value)
        {
            writer.WriteHalf(value);
        }
    }
}
