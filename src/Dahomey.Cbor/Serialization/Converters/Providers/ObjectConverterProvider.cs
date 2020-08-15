using System;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class ObjectConverterProvider : CborConverterProviderBase
    {
        public override ICborConverter? GetConverter(Type type, CborOptions options)
        {
            Type? nullableUnderlyingType = Nullable.GetUnderlyingType(type);

            if (nullableUnderlyingType != null)
            {
                return CreateGenericConverter(options, typeof(NullableConverter<>), nullableUnderlyingType);
            }

            if (type.IsClass || type.IsInterface)
            {
                return CreateGenericConverter(options, typeof(ObjectConverter<>), type);
            }

            return null;
        }
    }
}
