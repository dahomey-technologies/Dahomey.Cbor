using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class DiscriminatorMapping<T> : IMemberMapping where T : class
    {
        private readonly DiscriminatorConventionRegistry _discriminatorConventionRegistry;
        private readonly IObjectMapping _objectMapping;

        public MemberInfo MemberInfo => null;
        public Type MemberType => null;
        public string MemberName { get; private set; }
        public ICborConverter Converter => null;
        public bool CanBeDeserialized => false;
        public bool CanBeSerialized => true;
        public object DefaultValue => null;
        public bool IgnoreIfDefault => false;
        public Func<object, bool> ShouldSerializeMethod => null;
        public LengthMode LengthMode => LengthMode.Default;

        public DiscriminatorMapping(DiscriminatorConventionRegistry discriminatorConventionRegistry, 
            IObjectMapping objectMapping)
        {
            _discriminatorConventionRegistry = discriminatorConventionRegistry;
            _objectMapping = objectMapping;
        }

        public void Initialize()
        {
        }

        public void PostInitialize()
        {
            IDiscriminatorConvention discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);
            MemberName = Encoding.UTF8.GetString(discriminatorConvention.MemberName);
        }

        public IMemberConverter GenerateMemberConverter()
        {
            IDiscriminatorConvention discriminatorConvention = _discriminatorConventionRegistry.GetConvention(_objectMapping.ObjectType);

            IMemberConverter memberConverter = new DiscriminatorMemberConverter<T>(
                discriminatorConvention, _objectMapping.DiscriminatorPolicy);

            return memberConverter;
        }
    }
}
