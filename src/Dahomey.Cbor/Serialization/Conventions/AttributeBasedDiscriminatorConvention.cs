using System.Collections.Generic;
using System;
using System.Text;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Serialization.Converters;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class AttributeBasedDiscriminatorConvention<T> : IDiscriminatorConvention
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ReadOnlyMemory<byte> _memberName;
        private readonly Dictionary<T, Type> _typesByDiscriminator = new Dictionary<T, Type>();
        private readonly Dictionary<Type, T> _discriminatorsByType = new Dictionary<Type, T>();
        private readonly ICborConverter<T> _converter;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public AttributeBasedDiscriminatorConvention(SerializationRegistry serializationRegistry)
            : this(serializationRegistry, "_t")
        {
        }

        public AttributeBasedDiscriminatorConvention(SerializationRegistry serializationRegistry, string memberName)
        {
            _serializationRegistry = serializationRegistry;
            _memberName = Encoding.UTF8.GetBytes(memberName);
            _converter = serializationRegistry.ConverterRegistry.Lookup<T>();
        }

        public bool TryRegisterType(Type type)
        {
            IObjectMapping objectMapping = _serializationRegistry.ObjectMappingRegistry.Lookup(type);

            if (objectMapping.Discriminator == null || !(objectMapping.Discriminator is T discriminator))
            {
                return false;
            }

            _discriminatorsByType[type] = discriminator;
            _typesByDiscriminator.Add(discriminator, type);
            return true;
        }

        public Type ReadDiscriminator(ref CborReader reader)
        {
            T discriminator = _converter.Read(ref reader);
            if (!_typesByDiscriminator.TryGetValue(discriminator, out Type type))
            {
                throw new CborException($"Unknown type discriminator: {discriminator}");
            }
            return type;
        }

        public void WriteDiscriminator(ref CborWriter writer, Type actualType)
        {
            if (!_discriminatorsByType.TryGetValue(actualType, out T discriminator))
            {
                throw new CborException($"Unknown discriminator for type: {actualType}");
            }

            _converter.Write(ref writer, discriminator);
        }
    }
}
