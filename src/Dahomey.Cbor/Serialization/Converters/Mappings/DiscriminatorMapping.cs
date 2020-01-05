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

    public class DiscriminatorMapping<T> : IDiscriminatorMapping where T : class
    {
        private readonly DiscriminatorConventionRegistry _discriminatorConventionRegistry;
        private readonly IObjectMapping _objectMapping;
        private readonly Lazy<string> _memberName;

        public MemberInfo MemberInfo => null;
        public Type MemberType => null;
        public string MemberName => _memberName.Value;
        public ICborConverter Converter => null;
        public bool CanBeDeserialized => false;
        public bool CanBeSerialized => true;
        public object DefaultValue => null;
        public bool IgnoreIfDefault => false;
        public Func<object, bool> ShouldSerializeMethod => null;
        public LengthMode LengthMode => LengthMode.Default;
        public RequirementPolicy RequirementPolicy => RequirementPolicy.Never;

        public DiscriminatorMapping(DiscriminatorConventionRegistry discriminatorConventionRegistry, 
            IObjectMapping objectMapping)
        {
            _discriminatorConventionRegistry = discriminatorConventionRegistry;
            _objectMapping = objectMapping;
            _memberName = new Lazy<string>(GetMemberName);
        }

        public void Initialize()
        {
        }

        public void PostInitialize()
        {
        }

        public IMemberConverter GenerateMemberConverter()
        {
            IDiscriminatorConvention discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);

            IMemberConverter memberConverter = new DiscriminatorMemberConverter<T>(
                discriminatorConvention, _objectMapping.DiscriminatorPolicy);

            return memberConverter;
        }

        private string GetMemberName()
        {
            IDiscriminatorConvention discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);
            return Encoding.UTF8.GetString(discriminatorConvention.MemberName);
        }
    }
}
