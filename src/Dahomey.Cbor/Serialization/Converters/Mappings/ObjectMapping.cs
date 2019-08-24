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
            ReadOnlyMemory<byte> typeName = Encoding.ASCII.GetBytes($"{ObjectType.FullName}, {ObjectType.Assembly.GetName().Name}");
            CborDiscriminatorAttribute discriminatorAttribute = ObjectType.GetCustomAttribute<CborDiscriminatorAttribute>();

            ReadOnlyMemory<byte> discriminator = Encoding.UTF8.GetBytes(discriminatorAttribute != null ?
                discriminatorAttribute.Discriminator :
                $"{ObjectType.FullName}, {ObjectType.Assembly.GetName().Name}");

            _registry.DefaultDiscriminatorConvention.RegisterType(ObjectType, discriminator);

            Type namingConventionType = ObjectType.GetCustomAttribute<CborNamingConventionAttribute>()?.NamingConventionType;
            if (namingConventionType != null)
            {
                NamingConvention = (INamingConvention)Activator.CreateInstance(namingConventionType);
            }

            PropertyInfo[] properties = ObjectType.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo[] fields = ObjectType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (PropertyInfo propertyInfo in properties)
            {
                if (propertyInfo.IsDefined(typeof(CborIgnoreAttribute)))
                {
                    continue;
                }

                if ((propertyInfo.GetMethod.IsPrivate || propertyInfo.GetMethod.IsStatic) && ! propertyInfo.IsDefined(typeof(CborPropertyAttribute)))
                {
                    continue;
                }

                _memberMappings.Add(new MemberMapping(_registry.ConverterRegistry, this, propertyInfo, propertyInfo.PropertyType));
            }

            foreach (FieldInfo fieldInfo in fields)
            {
                if (fieldInfo.IsDefined(typeof(CborIgnoreAttribute)))
                {
                    continue;
                }

                if ((fieldInfo.IsPrivate || fieldInfo.IsStatic) && !fieldInfo.IsDefined(typeof(CborPropertyAttribute)))
                {
                    continue;
                }

                Type fieldType = fieldInfo.FieldType;

                _memberMappings.Add(new MemberMapping(_registry.ConverterRegistry, this, fieldInfo, fieldInfo.FieldType));
            }

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
