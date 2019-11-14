using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class DefaultDiscriminatorConvention : IDiscriminatorConvention
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ReadOnlyMemory<byte> _memberName;
        private readonly ByteBufferDictionary<Type> _typesByDiscriminator = new ByteBufferDictionary<Type>();
        private readonly Dictionary<Type, ReadOnlyMemory<byte>> _discriminatorsByType = new Dictionary<Type, ReadOnlyMemory<byte>>();

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry)
            : this(serializationRegistry, "_t")
        {
        }

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry, string memberName)
        {
            _serializationRegistry = serializationRegistry;
            _memberName = memberName.AsBinaryMemory();
        }

        public Type ReadDiscriminator(ref CborReader reader)
        {
            ReadOnlySpan<byte> discriminator = reader.ReadRawString();
            if (!_typesByDiscriminator.TryGetValue(discriminator, out Type type))
            {
                throw reader.BuildException($"Unknown type discriminator: {Encoding.UTF8.GetString(discriminator)}");
            }
            return type;
        }

        public void WriteDiscriminator<T>(ref CborWriter writer, Type actualType)
            where T : class
        {
            if (!_discriminatorsByType.TryGetValue(actualType, out ReadOnlyMemory<byte> discriminator))
            {
                throw new CborException($"Unknown discriminator for type: {actualType}");
            }

            writer.WriteString(discriminator.Span);
        }

        public bool TryRegisterType(Type type)
        {
            string discriminator = null;
            CborDiscriminatorAttribute discriminatorAttribute = type.GetCustomAttribute<CborDiscriminatorAttribute>();

            if (discriminatorAttribute != null)
            {
                discriminator = discriminatorAttribute.Discriminator;
            }
            else if (_serializationRegistry.ObjectMappingRegistry.TryLookup(type, out IObjectMapping objectMapping))
            {
                discriminator = objectMapping.Discriminator;
            }

            if (string.IsNullOrEmpty(discriminator))
            {
                return false;
            }

            ReadOnlyMemory<byte> discriminatorMemory = discriminator.AsBinaryMemory();
            _discriminatorsByType[type] = discriminatorMemory;
            _typesByDiscriminator.Add(discriminatorMemory.Span, type);
            return true;
        }
    }
}
