using Dahomey.Cbor.Attributes;
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
        private readonly ReadOnlyMemory<byte> _memberName = Encoding.ASCII.GetBytes("_t");
        private readonly ByteBufferDictionary<Type> _typesByDiscriminator = new ByteBufferDictionary<Type>();
        private readonly Dictionary<Type, ReadOnlyMemory<byte>> _discriminatorsByType = new Dictionary<Type, ReadOnlyMemory<byte>>();

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

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

        public void RegisterAssembly(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes()
                .Where(t => Attribute.IsDefined(t, typeof(CborDiscriminatorAttribute))))
            {
                string discriminator = type.GetCustomAttribute<CborDiscriminatorAttribute>().Discriminator;
                RegisterType(type, discriminator.AsBinaryMemory());
            }
        }

        public void RegisterType(Type type, ReadOnlyMemory<byte> discriminator)
        {
            _typesByDiscriminator.Add(discriminator.Span, type);
            _discriminatorsByType[type] = discriminator;
        }
    }
}
