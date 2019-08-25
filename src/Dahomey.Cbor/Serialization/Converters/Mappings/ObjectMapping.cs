using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    /// <summary>
    /// Represent a mapping between a class and Cbor serialization framework
    /// </summary>
    /// <typeparam name="T">The class.</typeparam>
    public class ObjectMapping<T> : IObjectMapping
        where T : class
    {
        private readonly SerializationRegistry _registry;
        private List<IMemberMapping> _memberMappings = new List<IMemberMapping>();

        public Type ObjectType { get; private set; }
        public INamingConvention NamingConvention { get; private set; }
        public IReadOnlyCollection<IMemberMapping> MemberMappings => _memberMappings;

        public ObjectMapping(SerializationRegistry registry)
        {
            _registry = registry;
            ObjectType = typeof(T);
        }

        public ObjectMapping<T> AutoMap()
        {
            IObjectMappingConvention convention = _registry.ObjectMappingConventionRegistry.Lookup<T>();
            convention.Apply<T>(_registry, this);
            return this;
        }

        public ObjectMapping<T> SetDiscriminator(ReadOnlyMemory<byte> discriminator)
        {
            _registry.DefaultDiscriminatorConvention.RegisterType(ObjectType, discriminator);
            return this;
        }

        public ObjectMapping<T> SetDiscriminator(string discriminator)
        {
            return SetDiscriminator(discriminator.AsBinaryMemory());
        }

        public ObjectMapping<T> SetNamingConvention(INamingConvention namingConvention)
        {
            NamingConvention = namingConvention;
            return this;
        }

        /// <summary>
        /// Creates a member map and adds it to the object mapping.
        /// </summary>
        /// <typeparam name="TM">The member type.</typeparam>
        /// <param name="memberLambda">A lambda expression specifying the member.</param>
        /// <returns>The member map.</returns>
        public MemberMapping MapMember<TM>(Expression<Func<T, TM>> memberLambda)
        {
            (MemberInfo memberInfo, Type memberType) = GetMemberInfoFromLambda(memberLambda);
            return MapMember(memberInfo, memberType);
        }

        public MemberMapping MapMember(MemberInfo memberInfo, Type memberType)
        {
            MemberMapping memberMapping = new MemberMapping(_registry.ConverterRegistry, this, memberInfo, memberType);
            _memberMappings.Add(memberMapping);
            return memberMapping;
        }

        public ObjectMapping<T> SetMemberMappings(IReadOnlyCollection<IMemberMapping> memberMappings)
        {
            _memberMappings = memberMappings.ToList();
            return this;
        }

        public ObjectMapping<T> ClearMemberMappings()
        {
            _memberMappings.Clear();
            return this;
        }

        private static (MemberInfo, Type) GetMemberInfoFromLambda<TM>(Expression<Func<T, TM>> memberLambda)
        {
            var body = memberLambda.Body;
            MemberExpression memberExpression;
            switch (body.NodeType)
            {
                case ExpressionType.MemberAccess:
                    memberExpression = (MemberExpression)body;
                    break;
                case ExpressionType.Convert:
                    var convertExpression = (UnaryExpression)body;
                    memberExpression = (MemberExpression)convertExpression.Operand;
                    break;
                default:
                    throw new InvalidOperationException("Invalid lambda expression");
            }

            MemberInfo memberInfo = memberExpression.Member;

            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    return (memberInfo, propertyInfo.PropertyType);

                case FieldInfo fieldfInfo:
                    return (memberInfo, fieldfInfo.FieldType);

                default:
                    throw new InvalidOperationException("Invalid lambda expression");

            }
        }
    }
}
