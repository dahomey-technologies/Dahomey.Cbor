using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class CollectionConverterProvider : CborConverterProviderBase
    {
        public override ICborConverter? GetConverter(Type type, CborOptions options)
        {
            if (type.IsArray)
            {
                Type? itemType = type.GetElementType();

                if (itemType == null)
                {
                    throw new CborException("Unexpected");
                }

                return CreateGenericConverter(
                    options,
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
                        options,
                        typeof(ImmutableDictionaryConverter<,,>), type, keyType, valueType);
                }

                if (type.GetInterfaces()
                    .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                {
                    Type keyType = type.GetGenericArguments()[0];
                    Type valueType = type.GetGenericArguments()[1];
                    return CreateGenericConverter(
                        options,
                        typeof(DictionaryConverter<,,>), type, keyType, valueType);
                }

                if (type.IsInterface
                    && type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    Type keyType = type.GetGenericArguments()[0];
                    Type valueType = type.GetGenericArguments()[1];
                    return CreateGenericConverter(
                        options,
                        typeof(InterfaceDictionaryConverter<,>),
                        keyType, valueType);
                }

                if (type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableList<>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableSortedSet<>)
                    || type.GetGenericTypeDefinition() == typeof(ImmutableHashSet<>))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return CreateGenericConverter(
                        options,
                        typeof(ImmutableCollectionConverter<,>), type, itemType);
                }

                if (type.IsInterface
                    && type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(ISet<>))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return CreateGenericConverter(
                        options,
                        typeof(InterfaceCollectionConverter<,,>),
                        typeof(HashSet<>).MakeGenericType(itemType), type, itemType);
                }

                if (type.IsInterface
                    && type.IsGenericType
                    && (type.GetGenericTypeDefinition() == typeof(IList<>)
                        || type.GetGenericTypeDefinition() == typeof(ICollection<>)
                        || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        || type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
                        || type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return CreateGenericConverter(
                        options,
                        typeof(InterfaceCollectionConverter<,,>),
                        typeof(List<>).MakeGenericType(itemType), type, itemType);
                }

                if (type.GetInterfaces()
                    .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return CreateGenericConverter(
                        options,
                        typeof(CollectionConverter<,>), type, itemType);
                }
            }

            return null;
        }
    }
}
