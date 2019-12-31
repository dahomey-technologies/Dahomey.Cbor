namespace Dahomey.Cbor.Serialization.Converters
{
    public sealed class NullableConverter<T> : CborConverterBase<T?> where T : struct
    {
        private readonly ICborConverter<T> _cborConverter;

        public NullableConverter(SerializationRegistry registry)
        {
            this._cborConverter = registry.ConverterRegistry.Lookup<T>();
        }

        public override T? Read(ref CborReader reader)
        { 
            if (reader.ReadNull())
            {
                return default;
            }

            return this._cborConverter.Read(ref reader);
        }


        public override void Write(ref CborWriter writer, T? value)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            this._cborConverter.Write(ref writer, value.Value);
        }
    }
}
