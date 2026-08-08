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
        private readonly Type? _fallbackType;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry)
            : this(serializationRegistry, "_t")
        {
        }

        public DefaultDiscriminatorConvention(SerializationRegistry serializationRegistry, string memberName)
            : this(serializationRegistry, memberName, null)
        {
        }

        /// <param name="fallbackType">
        /// Type to resolve to when a discriminator value has no registered type — typically data
        /// written by a newer build that added a subtype this build does not know about. Leave null to
        /// throw on an unknown discriminator, which is the default.
        /// </param>
        /// <remarks>
        /// The fallback must be assignable to the declared type being read, or the read fails with the
        /// usual "is not assignable from" error. Members the fallback does not declare are handled by
        /// <see cref="CborOptions.UnhandledNameMode"/> — so forward compatibility generally also wants
        /// <see cref="UnhandledNameMode.Silent"/>, otherwise the unknown subtype's extra members throw
        /// instead of being skipped.
        /// </remarks>
        /// <param name="serializationRegistry">The registry the convention resolves types through.</param>
        /// <param name="memberName">Name of the member carrying the discriminator.</param>
        public DefaultDiscriminatorConvention(
            SerializationRegistry serializationRegistry, string memberName, Type? fallbackType)
        {
            _serializationRegistry = serializationRegistry;
            _memberName = memberName.AsBinaryMemory();
            _converter = serializationRegistry.ConverterRegistry.Lookup<T>();
            _fallbackType = fallbackType;
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

            if (_typesByDiscriminator.TryGetValue(discriminator, out Type? type))
            {
                return type;
            }

            if (_fallbackType != null)
            {
                return _fallbackType;
            }

            throw new CborException($"Unknown type discriminator: {discriminator}");
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
