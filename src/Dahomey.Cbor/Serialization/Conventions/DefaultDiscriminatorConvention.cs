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

            _typesByDiscriminator.AddOrUpdate(
                discriminator, type, (_, registered) => ResolveSharedDiscriminator(registered, type, discriminator));
            _discriminatorsByType.TryAdd(type, discriminator);
            return true;
        }

        /// <summary>
        /// Picks the type a discriminator value reads as when two types carry it, or refuses the pair.
        /// </summary>
        /// <remarks>
        /// A subtype with no discriminator attribute of its own inherits its base's value, so it writes
        /// that value too. That has always been allowed, and the value keeps reading as the type
        /// registered first: a program that only uses the subtype reads it back as itself, which
        /// handing the value to the base would break. Anything else is two types claiming one value -
        /// unrelated types, or a subtype declaring its base's value again - and a document carrying it
        /// would read as whichever came first, so it is refused.
        /// </remarks>
        private static Type ResolveSharedDiscriminator(Type registered, Type type, T discriminator)
        {
            if (registered == type)
            {
                return registered;
            }

            Type? derivedType = registered.IsAssignableFrom(type) ? type
                : type.IsAssignableFrom(registered) ? registered
                : null;

            if (derivedType != null && !DiscriminatorConventionRegistry.DeclaresDiscriminator(derivedType))
            {
                return registered;
            }

            throw new CborException(
                $"Types {registered} and {type} have the same discriminator {discriminator}: "
                + "a document carrying it cannot tell them apart. Give each type its own value.");
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
