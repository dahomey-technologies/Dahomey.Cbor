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
        private bool _isInitialized = false;
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

        public void EnsureInitialize()
        {
            if (!_isInitialized)
            {
                lock (this)
                {
                    if (!_isInitialized)
                    {
                        IDiscriminatorConvention? discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);
                        if (discriminatorConvention != null)
                        {
                            _memberName = Encoding.UTF8.GetString(discriminatorConvention.MemberName);
                        }
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

            int? memberIndex = _objectMapping.ObjectFormat == CborObjectFormat.Array ? 0 : null;

            IMemberConverter memberConverter = new DiscriminatorMemberConverter<T>(
                _options, discriminatorConvention, _objectMapping.DiscriminatorPolicy, _objectMapping.ObjectFormat);

            return memberConverter;
        }
    }
}
