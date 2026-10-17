namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// <see cref="System.Half"/> as RFC 8949's binary16 — major type 7, additional value 25.
    /// </summary>
    /// <remarks>
    /// A <c>Half</c> needs a concrete converter or it reaches <c>ObjectConverterProvider</c> and is
    /// mapped like any other struct — written as the struct's own internal members and read back as
    /// <c>default</c>, because every one of them is computed or read-only. That failure is silent in
    /// both directions, which is what makes the registration in <c>PrimitiveConverterProvider</c>
    /// load-bearing rather than a convenience.
    /// <para>
    /// Both directions defer to <see cref="CborReader"/> and <see cref="CborWriter"/>, which already
    /// carry the half-float primitive: the reader accepts the same shapes <c>ReadSingle</c> does — an
    /// integer, a text string, any of the float widths — so a <c>Half</c> member is exactly as tolerant
    /// of what a sender chose as a <c>float</c> member is, rather than having a policy of its own. The
    /// writer emits binary16 and nothing wider, where <c>WriteSingle</c> and <c>WriteDouble</c> pick the
    /// shortest form that round trips; a NaN of any payload goes out as the canonical <c>F9 7E00</c>.
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
