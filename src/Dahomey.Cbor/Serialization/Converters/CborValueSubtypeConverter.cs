using Dahomey.Cbor.ObjectModel;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes one concrete object-model type, such as <see cref="CborString"/>, through
    /// <see cref="CborValueConverter"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CborValueConverter"/> is a converter of <see cref="CborValue"/>, <see cref="CborObject"/>
    /// and <see cref="CborArray"/> only, so handing it out for <c>CborString</c> made
    /// <c>Cbor.Deserialize&lt;CborString&gt;</c> fail with <see cref="System.InvalidCastException"/>.
    /// A data item of another kind is refused with a <see cref="CborException"/>; <c>null</c> reads as
    /// <c>null</c>, as it does for <see cref="CborObject"/>.
    /// </remarks>
    public sealed class CborValueSubtypeConverter<T> : CborConverterBase<T?>
        where T : CborValue
    {
        private readonly CborValueConverter _converter;

        public CborValueSubtypeConverter(CborOptions options)
        {
            _converter = new CborValueConverter(options);
        }

        public override T? Read(ref CborReader reader)
        {
            CborValue value = _converter.Read(ref reader);

            if (value is T typed)
            {
                return typed;
            }

            if (value.Type == CborValueType.Null)
            {
                return null;
            }

            throw reader.BuildException($"Expected a {typeof(T).Name}, got a {value.GetType().Name}");
        }

        public override void Write(ref CborWriter writer, T? value, LengthMode lengthMode)
        {
            _converter.Write(ref writer, value!, lengthMode);
        }
    }
}
