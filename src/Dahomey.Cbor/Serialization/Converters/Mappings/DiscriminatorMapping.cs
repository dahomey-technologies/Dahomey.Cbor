using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IDiscriminatorMapping : IMemberMapping
    {
    }

    public class DiscriminatorMapping<T> : IDiscriminatorMapping
    {
        /// <remarks>
        /// The registry <see cref="DiscriminatorConventionRegistry.Version"/> that <see cref="_memberName"/>
        /// was resolved at, or -1 before the first resolution. Volatile because
        /// <see cref="EnsureInitialize"/> reads it outside the lock and the caller goes straight on to
        /// read <see cref="_memberName"/>: the name is written first and the version after, and a reader
        /// has to see them in that order, or <see cref="MemberName"/> answers a stale name for a mapping
        /// that is in fact current.
        /// </remarks>
        private volatile int _initializedVersion = -1;
        private readonly object _lock = new object();
        private readonly CborOptions _options;
        private readonly DiscriminatorConventionRegistry _discriminatorConventionRegistry;
        private readonly IObjectMapping _objectMapping;
        private string? _memberName = null;

        public MemberInfo? MemberInfo => null;
        public Type MemberType => throw new NotSupportedException();
        public int? MemberIndex => 0; // discriminator gets always the first index
        public string? MemberName
        {
            get
            {
                EnsureInitialize();
                return _memberName;
            }
        }
        public ICborConverter Converter => throw new NotSupportedException();
        public bool CanBeDeserialized => false;
        public bool CanBeSerialized => true;
        public object DefaultValue => throw new NotSupportedException();
        public bool IgnoreIfDefault => false;
        public Func<object, bool> ShouldSerializeMethod => throw new NotSupportedException();
        public LengthMode LengthMode => LengthMode.Default;
        public RequirementPolicy RequirementPolicy => RequirementPolicy.Never;

        public DiscriminatorMapping(CborOptions options, IObjectMapping objectMapping)
        {
            _options = options;
            _discriminatorConventionRegistry = options.Registry.DiscriminatorConventionRegistry;
            _objectMapping = objectMapping;
        }

        /// <remarks>
        /// Resolved again whenever the registry's version has moved. The name was once latched on the
        /// first non-null answer on the grounds that a resolved convention never changes, which holds
        /// until <see cref="DiscriminatorConventionRegistry.ClearConventions"/>: a different convention
        /// may then govern the type under another member name. A type with no convention resolves to
        /// <c>null</c> and is simply stamped, so asking again costs one int compare until something is
        /// registered.
        /// </remarks>
        public void EnsureInitialize()
        {
            if (_initializedVersion != _discriminatorConventionRegistry.Version)
            {
                lock (_lock)
                {
                    // Sampled before the lookup, so a registration landing in between is seen next time.
                    int version = _discriminatorConventionRegistry.Version;

                    if (_initializedVersion != version)
                    {
                        IDiscriminatorConvention? discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);
                        _memberName = discriminatorConvention != null
                            ? Encoding.UTF8.GetString(discriminatorConvention.MemberName)
                            : null;

                        _initializedVersion = version;
                    }
                }
            }
        }

        public IMemberConverter GenerateMemberConverter()
        {
            IDiscriminatorConvention? discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);

            if (discriminatorConvention == null)
            {
                throw new CborException($"Cannot find a discriminator convention for type {_objectMapping.ObjectType}");
            }

            IMemberConverter memberConverter = new DiscriminatorMemberConverter<T>(
                _options, discriminatorConvention, _objectMapping.DiscriminatorPolicy, _objectMapping.ObjectFormat, _objectMapping);

            return memberConverter;
        }

        public string? GetMemberNameForConverter(ICborConverter converter)
        {
            EnsureInitialize();
            return _memberName;
        }
    }
}
