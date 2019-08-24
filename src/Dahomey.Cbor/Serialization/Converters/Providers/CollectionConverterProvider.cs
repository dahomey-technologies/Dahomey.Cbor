using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class CollectionConverterProvider : CborConverterProviderBase
    {
        public override ICborConverter GetConverter(Type type, SerializationRegistry registry)
        {
            if (type.IsArray)
            {
                Type itemType = type.GetElementType();
                return CreateGenericConverter(
                    registry,
                    typeof(ArrayConverter<>), itemType);
            }

            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(ImmutableDictionary<,>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableSortedDictionary<,>))
                {
                    Type keyType = type.GetGenericArguments()[0];
                    Type valueType = type.GetGenericArguments()[1];
                    return CreateGenericConverter(
                        registry,
                        typeof(ImmutableDictionaryConverter<,,>), type, keyType, valueType);
                }

                if (type.GetInterfaces()
                    .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                {
                    Type keyType = type.GetGenericArguments()[0];
                    Type valueType = type.GetGenericArguments()[1];
                    return CreateGenericConverter(
                        registry,
                        typeof(DictionaryConverter<,,>), type, keyType, valueType);
                }

                if (type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableList<>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableSortedSet<>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableHashSet<>))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return CreateGenericConverter(
                        registry,
                        typeof(ImmutableCollectionConverter<,>), type, itemType);
                }

                if (type.GetInterfaces()
                    .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return CreateGenericConverter(
                        registry, 
                        typeof(CollectionConverter<,>), type, itemType);
                }
            }

            return null;
        }
    }
}
