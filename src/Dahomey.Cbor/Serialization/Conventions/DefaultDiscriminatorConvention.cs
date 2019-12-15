using Dahomey.Cbor.Util;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class DefaultDiscriminatorConvention : IDiscriminatorConvention
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ReadOnlyMemory<byte> _memberName;
        private readonly ConcurrentDictionary<string, Type> _typesByDiscriminator = new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<Type, string> _discriminatorsByType = new ConcurrentDictionary<Type, string>();

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


        public bool TryRegisterType(Type type)
        {
            return true;
        }

        public Type ReadDiscriminator(ref CborReader reader)
        {
            string discriminator = reader.ReadString();
            Type type = _typesByDiscriminator.GetOrAdd(discriminator, NameToType);
            return type;
        }

        public void WriteDiscriminator(ref CborWriter writer, Type actualType)
        {
            string discriminator = _discriminatorsByType.GetOrAdd(actualType, TypeToName);
            writer.WriteString(discriminator);
        }

        private string TypeToName(Type type)
        {
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        private Type NameToType(string name)
        {
            string[] parts = name.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string assemblyName;
            string typeName;

            switch (parts.Length)
            {
                case 1:
                    typeName = parts[0];
                    assemblyName = null;
                    break;

                case 2:
                    typeName = parts[0];
                    assemblyName = parts[1];
                    break;

                default:
                    throw new CborException($"Invalid discriminator {name}");

            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                Assembly assembly = Assembly.Load(assemblyName);
                Type type = assembly.GetType(typeName);
                return type;
            }

            return Type.GetType(typeName);
        }
    }
}
