using System;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class ObjectConverterProvider : CborConverterProviderBase
    {
        public override ICborConverter? GetConverter(Type type, SerializationRegistry registry)
        {
            Type? nullableUnderlyingType = Nullable.GetUnderlyingType(type);

            if (nullableUnderlyingType != null)
            {
                return CreateGenericConverter(registry, typeof(NullableConverter<>), nullableUnderlyingType);
            }

            if (type.IsClass || type.IsInterface)
            {
                return CreateGenericConverter(registry, typeof(ObjectConverter<>), type);
            }

            return null;
        }
    }
}
