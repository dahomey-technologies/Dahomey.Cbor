using Dahomey.Cbor.Serialization.Converters;
using System;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class TypedArrayConverterProvider : CborConverterProviderBase
    {
        public override ICborConverter? GetConverter(Type type, CborOptions options)
        {
            if (!type.IsArray || type.GetArrayRank() != 1)
            {
                return null;
            }

            Type? itemType = type.GetElementType();

            if (itemType == null || !TypedArrayTags.TryGetByElementType(itemType, out _))
            {
                return null;
            }

            return CreateGenericConverter(options, typeof(TypedArrayConverter<>), itemType);
        }
    }
}
