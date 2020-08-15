using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public abstract class CborConverterProviderBase : ICborConverterProvider
    {
        public abstract ICborConverter? GetConverter(Type type, CborOptions options);

        protected ICborConverter CreateConverter(CborOptions options, Type converterType)
        {
            ConstructorInfo? constructorInfo = converterType.GetConstructor(new[] { typeof(CborOptions) });
            if (constructorInfo != null)
            {
                ICborConverter? converter = (ICborConverter?)Activator.CreateInstance(converterType, options);

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

        protected ICborConverter CreateGenericConverter(CborOptions options, Type genericType, params Type[] typeArguments)
        {
            return CreateConverter(options, genericType.MakeGenericType(typeArguments));
        }
    }
}
