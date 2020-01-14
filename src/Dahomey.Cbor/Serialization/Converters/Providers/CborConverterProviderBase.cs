using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public abstract class CborConverterProviderBase : ICborConverterProvider
    {
        public abstract ICborConverter? GetConverter(Type type, SerializationRegistry registry);

        protected ICborConverter CreateConverter(SerializationRegistry registry, Type converterType)
        {
            ConstructorInfo? constructorInfo = converterType.GetConstructor(new[] { typeof(SerializationRegistry) });
            if (constructorInfo != null)
            {
                ICborConverter? converter = (ICborConverter?)Activator.CreateInstance(converterType, registry);

                if (converter == null)
                {
                    throw new CborException($"Cannot instantiate {converterType}");
                }

                return converter;
            }

            constructorInfo = converterType.GetConstructor(new Type[0]);
            if (constructorInfo != null)
            {
                ICborConverter? converter = (ICborConverter?)Activator.CreateInstance(converterType);

                if (converter == null)
                {
                    throw new CborException($"Cannot instantiate {converterType}");
                }

                return converter;
            }

            throw new MissingMethodException(
                $"No suitable constructor found for converter type: '{converterType.FullName}'.");
        }

        protected ICborConverter CreateGenericConverter(SerializationRegistry registry, Type genericType, params Type[] typeArguments)
        {
            return CreateConverter(registry, genericType.MakeGenericType(typeArguments));
        }
    }
}
