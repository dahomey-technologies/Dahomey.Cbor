using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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
        private ICreatorMapping _creatorMapping = null;

        public Type ObjectType { get; private set; }

        public INamingConvention NamingConvention { get; private set; }
        public IReadOnlyCollection<IMemberMapping> MemberMappings => _memberMappings;
        public ICreatorMapping CreatorMapping => _creatorMapping;

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

        public CreatorMapping MapCreator(ConstructorInfo constructorInfo)
        {
            if (constructorInfo == null)
            {
                throw new ArgumentNullException("constructorInfo");
            }

            CreatorMapping creatorMapping = new CreatorMapping(this, constructorInfo);
            _creatorMapping = creatorMapping;
            return creatorMapping;
        }


        private CreatorMapping MapCreator(MethodInfo method)
        {
            if (method == null)
            {
                throw new ArgumentNullException("method");
            }

            CreatorMapping creatorMapping = new CreatorMapping(this, method);
            _creatorMapping = creatorMapping;
            return creatorMapping;
        }

        public CreatorMapping MapCreator(Delegate creatorFunc)
        {
            if (creatorFunc == null)
            {
                throw new ArgumentNullException("creatorFunc");
            }

            CreatorMapping creatorMapping = new CreatorMapping(this, creatorFunc);
            _creatorMapping = creatorMapping;
            return creatorMapping;
        }

        public CreatorMapping MapCreator(Expression<Func<T, T>> creatorLambda)
        {
            if (creatorLambda == null)
            {
                throw new ArgumentNullException("creatorLambda");
            }

            if (creatorLambda.Body is NewExpression newExpression)
            {
                return MapCreator(newExpression.Constructor);
            }
            else if (creatorLambda.Body is MethodCallExpression methodCallExpression && methodCallExpression.Object == null)
            {
                return MapCreator(methodCallExpression.Method);
            }

            throw new ArgumentException("creatorLambda should be a 'new' or a static 'method call' expression");
        }

        public ObjectMapping<T> SetCreatorMapping(ICreatorMapping creatorMapping)
        {
            _creatorMapping = creatorMapping;
            return this;
        }

        public void Initialize()
        {
            foreach(IMemberMapping mapping in _memberMappings)
            {
                if (mapping is IMappingInitialization memberInitialization)
                {
                    memberInitialization.Initialize();
                }
            }

            if (CreatorMapping != null && CreatorMapping is IMappingInitialization creatorInitialization)
            {
                creatorInitialization.Initialize();
            }
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
