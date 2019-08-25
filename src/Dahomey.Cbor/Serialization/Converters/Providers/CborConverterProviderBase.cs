using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public abstract class CborConverterProviderBase : ICborConverterProvider
    {
        public abstract ICborConverter GetConverter(Type type, SerializationRegistry registry);

        protected ICborConverter CreateConverter(SerializationRegistry registry, Type converterType)
        {
            ConstructorInfo constructorInfo = converterType.GetConstructor(new[] { typeof(SerializationRegistry) });
            if (constructorInfo != null)
            {
                return (ICborConverter)Activator.CreateInstance(converterType, registry);
            }

            constructorInfo = converterType.GetConstructor(new Type[0]);
            if (constructorInfo != null)
            {
                return (ICborConverter)Activator.CreateInstance(converterType);
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
