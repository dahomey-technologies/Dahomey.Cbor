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
        private static readonly ReadOnlyMemory<byte> _memberName = Encoding.ASCII.GetBytes("_t");
        private static readonly ByteBufferDictionary<Type> _typesByDiscriminator = new ByteBufferDictionary<Type>();
        private static readonly Dictionary<Type, ReadOnlyMemory<byte>> _discriminatorsByType = new Dictionary<Type, ReadOnlyMemory<byte>>();

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

            writer.WriteString(_memberName.Span);
            writer.WriteString(discriminator.Span);
        }

        public static void RegisterAssembly(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes()
                .Where(t => Attribute.IsDefined(t, typeof(CborDiscriminatorAttribute))))
            {
                string discriminator = type.GetCustomAttribute<CborDiscriminatorAttribute>().Discriminator;
                _typesByDiscriminator.Add(discriminator.AsBinarySpan(), type);
                _discriminatorsByType.Add(type, discriminator.AsBinaryMemory());
            }
        }
    }
}
