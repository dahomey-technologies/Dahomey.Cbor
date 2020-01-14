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

        public MemberInfo MemberInfo => throw new NotSupportedException();
        public Type MemberType => throw new NotSupportedException();
        public string? MemberName { get; private set; }
        public ICborConverter Converter => throw new NotSupportedException();
        public bool CanBeDeserialized => false;
        public bool CanBeSerialized => true;
        public object DefaultValue => throw new NotSupportedException();
        public bool IgnoreIfDefault => false;
        public Func<object, bool> ShouldSerializeMethod => throw new NotSupportedException();
        public LengthMode LengthMode => LengthMode.Default;
        public RequirementPolicy RequirementPolicy => RequirementPolicy.Never;

        public DiscriminatorMapping(DiscriminatorConventionRegistry discriminatorConventionRegistry, 
            IObjectMapping objectMapping)
        {
            _discriminatorConventionRegistry = discriminatorConventionRegistry;
            _objectMapping = objectMapping;
        }

        public void Initialize()
        {
            IDiscriminatorConvention? discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);
            if (discriminatorConvention != null)
            {
                MemberName = Encoding.UTF8.GetString(discriminatorConvention.MemberName);
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
                discriminatorConvention, _objectMapping.DiscriminatorPolicy);

            return memberConverter;
        }
    }
}
