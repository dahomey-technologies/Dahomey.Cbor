using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Converters;
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
        private readonly ReadOnlyMemory<byte> _memberName = Encoding.ASCII.GetBytes("_t");
        private readonly ByteBufferDictionary<Type> _typesByDiscriminator = new ByteBufferDictionary<Type>();
        private readonly Dictionary<Type, ReadOnlyMemory<byte>> _discriminatorsByType = new Dictionary<Type, ReadOnlyMemory<byte>>();

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry)
        {
            _serializationRegistry = serializationRegistry;
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

            writer.WriteString(MemberName);
            writer.WriteString(discriminator.Span);
        }

        public void RegisterType(Type type, ReadOnlyMemory<byte> discriminator)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (discriminator.Length == 0)
            {
                throw new ArgumentException(nameof(discriminator));
            }

            _discriminatorsByType[type] = discriminator;

            // setup discriminator for type and all its base types
            for (Type currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                IObjectConverter objectConverter = _serializationRegistry.ConverterRegistry.Lookup(type) as IObjectConverter;
                objectConverter.SetDiscriminatorConvention(this);
            }
        }

        public void RegisterType(Type type, string discriminator)
        {
            if (string.IsNullOrEmpty(discriminator))
            {
                throw new ArgumentNullException(nameof(discriminator));
            }

            RegisterType(type, discriminator.AsBinaryMemory());
        }

        public bool TryRegisterType(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            CborDiscriminatorAttribute attribute = type.GetCustomAttribute<CborDiscriminatorAttribute>();

            if (attribute == null)
            {
                return false;
            }

            RegisterType(type, attribute.Discriminator);
            return true;
        }
    }
}
