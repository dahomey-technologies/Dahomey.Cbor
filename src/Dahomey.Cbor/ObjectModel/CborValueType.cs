namespace Dahomey.Cbor.ObjectModel
{
    public enum CborValueType
    {
        /// <summary>
        /// The zero sentinel, not a value any <see cref="CborValue"/> reports. In particular it is not
        /// CBOR's <c>undefined</c> (<c>F7</c>): that decodes to <see cref="CborValue.Null"/> and so
        /// reports <see cref="Null"/>, which makes <c>undefined</c> and <c>null</c> indistinguishable
        /// after a round trip. Giving <c>F7</c> a value of its own would be a modelling change rather
        /// than a fix, and would need a matching case in <c>CborValueConverter.Write</c>, whose switch
        /// has no default and would otherwise write nothing at all for it.
        /// </summary>
        Undefined =  0,
        Object,
        Array,
        Positive,
        Negative,
        Single,
        Double,
        Decimal,
        String,
        Boolean,
        Null,
        ByteString
    }
}
