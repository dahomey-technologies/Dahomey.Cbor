using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Concurrent;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class DefaultDiscriminatorConvention<T> : IDiscriminatorConvention
        where T : notnull
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ReadOnlyMemory<byte> _memberName;
        private readonly ConcurrentDictionary<T, Type> _typesByDiscriminator = new();
        private readonly ConcurrentDictionary<Type, T> _discriminatorsByType = new();
        private readonly ICborConverter<T> _converter;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry)
            : this(serializationRegistry, "_t")
        {
        }

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry, string memberName)
        {
            _serializationRegistry = serializationRegistry;
            _memberName = memberName.AsBinaryMemory();
            _converter = serializationRegistry.ConverterRegistry.Lookup<T>();
        }


        public bool TryRegisterType(Type type)
        {
            IObjectMapping objectMapping = _serializationRegistry.ObjectMappingRegistry.Lookup(type);

            if (objectMapping.Discriminator == null || objectMapping.Discriminator is not T discriminator)
            {
                return false;
            }

            _discriminatorsByType.TryAdd(type, discriminator);
            _typesByDiscriminator.TryAdd(discriminator, type);
            return true;
        }

        public Type ReadDiscriminator(ref CborReader reader)
        {
            T discriminator = _converter.Read(ref reader);

            if (discriminator == null)
            {
                throw new CborException("Null discriminator");
            }

            if (!_typesByDiscriminator.TryGetValue(discriminator, out Type? type))
            {
                throw new CborException($"Unknown type discriminator: {discriminator}");
            }

            return type;
        }

        public void WriteDiscriminator(ref CborWriter writer, Type actualType)
        {
            if (!_discriminatorsByType.TryGetValue(actualType, out T? discriminator))
            {
                throw new CborException($"Unknown discriminator for type: {actualType}");
            }

            _converter.Write(ref writer, discriminator);
        }
    }
}
