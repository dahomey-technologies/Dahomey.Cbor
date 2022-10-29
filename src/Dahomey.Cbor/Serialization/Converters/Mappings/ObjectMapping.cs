using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    /// <summary>
    /// Represent a mapping between a class and Cbor serialization framework
    /// </summary>
    /// <typeparam name="T">The class.</typeparam>
    public class ObjectMapping<T> : IObjectMapping
    {
        private bool _isInitialized = false;
        private readonly SerializationRegistry _registry;
        private readonly CborOptions _options;
        private List<IMemberMapping> _memberMappings = new List<IMemberMapping>();
        private ICreatorMapping? _creatorMapping = null;
        private Action? _orderByAction = null;

        public Type ObjectType { get; private set; }

        public INamingConvention? NamingConvention { get; private set; }
        public IReadOnlyCollection<IMemberMapping> MemberMappings
        {
            get
            {
                EnsureInitialize();
                return _memberMappings;
            }
        }
        public ICreatorMapping? CreatorMapping
        {
            get
            {
                EnsureInitialize();
                return _creatorMapping;
            }
        }
        public Delegate? OnSerializingMethod { get; private set; }
        public Delegate? OnSerializedMethod { get; private set; }
        public Delegate? OnDeserializingMethod { get; private set; }
        public Delegate? OnDeserializedMethod { get; private set; }
        public CborDiscriminatorPolicy DiscriminatorPolicy { get; private set; }
        public object? Discriminator { get; private set; }
        public LengthMode LengthMode { get; private set; }
        public CborObjectFormat ObjectFormat { get; private set; }

        public ObjectMapping(SerializationRegistry registry, CborOptions options)
        {
            _registry = registry;
            _options = options;
            ObjectType = typeof(T);
            ObjectFormat = options.ObjectFormat; // default value
        }

        void IObjectMapping.AutoMap()
        {
            AutoMap();
        }

        public ObjectMapping<T> AutoMap()
        {
            IObjectMappingConvention convention = _registry.ObjectMappingConventionRegistry.Lookup<T>();
            convention.Apply<T>(_registry, this);
            return this;
        }

        public ObjectMapping<T> SetDiscriminator(object discriminator)
        {
            Discriminator = discriminator;

            if (discriminator != null
                && !ObjectType.IsAbstract 
                && !ObjectType.IsInterface && !ObjectType.IsStruct()
                && _registry.DiscriminatorConventionRegistry.AnyConvention()
                && (_memberMappings.Count == 0 || _memberMappings[0] is not DiscriminatorMapping<T>))
            {
                DiscriminatorMapping<T> memberMapping = new DiscriminatorMapping<T>(_registry.DiscriminatorConventionRegistry, this);
                _memberMappings.Insert(0, memberMapping);
            }

            return this;
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
        public MemberMapping<T> MapMember<TM>(Expression<Func<T, TM>> memberLambda)
        {
            (MemberInfo memberInfo, Type memberType) = GetMemberInfoFromLambda(memberLambda);
            return MapMember(memberInfo, memberType);
        }

        public MemberMapping<T> MapMember(MemberInfo memberInfo, Type memberType)
        {
            MemberMapping<T> memberMapping = new MemberMapping<T>(_registry.ConverterRegistry, this, memberInfo, memberType);
            _memberMappings.Add(memberMapping);
            return memberMapping;
        }

        public MemberMapping<T> MapMember(FieldInfo fieldInfo)
        {
            return MapMember(fieldInfo, fieldInfo.FieldType);
        }

        public MemberMapping<T> MapMember(PropertyInfo propertyInfo)
        {
            return MapMember(propertyInfo, propertyInfo.PropertyType);
        }

        public ObjectMapping<T> AddMemberMappings(IReadOnlyCollection<IMemberMapping> memberMappings)
        {
            _memberMappings.AddRange(memberMappings);
            return this;
        }

        public ObjectMapping<T> SetMemberMappings(IEnumerable<IMemberMapping> memberMappings)
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

            if (creatorLambda.Body is NewExpression newExpression && newExpression.Constructor != null)
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

        public ObjectMapping<T> SetOnSerializingMethod(Action<T> onSerializingMethod)
        {
            OnSerializingMethod = onSerializingMethod;
            return this;
        }

        public ObjectMapping<T> SetOnSerializedMethod(Action<T> onSerializedMethod)
        {
            OnSerializedMethod = onSerializedMethod;
            return this;
        }

        public ObjectMapping<T> SetOnDeserializingMethod(Action<T> onDeserializingMethod)
        {
            OnDeserializingMethod = onDeserializingMethod;
            return this;
        }

        public ObjectMapping<T> SetOnDeserializedMethod(Action<T> onDeserializedMethod)
        {
            OnDeserializedMethod = onDeserializedMethod;
            return this;
        }

        public ObjectMapping<T> SetDiscriminatorPolicy(CborDiscriminatorPolicy discriminatorPolicy)
        {
            DiscriminatorPolicy = discriminatorPolicy;

            if ((discriminatorPolicy == CborDiscriminatorPolicy.Always
                || discriminatorPolicy == CborDiscriminatorPolicy.Default && _options.DiscriminatorPolicy == CborDiscriminatorPolicy.Always)
                && !ObjectType.IsAbstract
                && !ObjectType.IsInterface && !ObjectType.IsStruct()
                && _registry.DiscriminatorConventionRegistry.AnyConvention()
                && (_memberMappings.Count == 0 || _memberMappings[0] is not DiscriminatorMapping<T>))
            {
                DiscriminatorMapping<T> memberMapping = new DiscriminatorMapping<T>(_registry.DiscriminatorConventionRegistry, this);
                _memberMappings.Insert(0, memberMapping);
            }

            return this;
        }

        public ObjectMapping<T> SetLengthMode(LengthMode lengthMode)
        {
            LengthMode = lengthMode;
            return this;
        }

        public void SetOrderBy<TP>(Func<IMemberMapping, TP> propertySelector)
        {
            _orderByAction = () =>
            {
                _memberMappings = _memberMappings
                    .OrderBy(propertySelector)
                    .ToList();
            };
        }

        public bool IsCreatorMember(ReadOnlySpan<byte> memberName)
        {
            if (CreatorMapping == null || CreatorMapping.MemberNames == null)
            {
                return false;
            }

            foreach (RawString creatorMemberName in CreatorMapping.MemberNames)
            {
                if (creatorMemberName.Buffer.Span.SequenceEqual(memberName))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsCreatorMember(int memberIndex)
        {
            if (CreatorMapping == null || CreatorMapping.MemberIndexes == null)
            {
                return false;
            }

            foreach (int creatorMemberIndex in CreatorMapping.MemberIndexes)
            {
                if (creatorMemberIndex == memberIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The class/struct will be serialized as a CBOR array instead of a CBOR map
        /// </summary>
        /// CborPropertyAttribute.Index will be used as the index of each property/field of the class in the CBOR array
        public void SetObjectFormat(CborObjectFormat objectFormat)
        {
            ObjectFormat = objectFormat;
        }

        private void EnsureInitialize()
        {
            if (!_isInitialized)
            {
                lock (this)
                {
                    if (!_isInitialized)
                    {
                        _orderByAction?.Invoke();
                        ValidateMemberNamesAndindexes();

                        _isInitialized = true;
                    }
                }
            }
        }

        private void ValidateMemberNamesAndindexes()
        {
            int memberNameCount = _memberMappings.Count(m => m.MemberName != null);
            int memberIndexCount = _memberMappings.Count(m => m.MemberIndex.HasValue);

            switch (ObjectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                    {
                        if (memberNameCount != _memberMappings.Count)
                        {
                            throw new CborException($"expecting all fields/properties to get a member name in class/struct {ObjectType.Name}");
                        }
                    }
                    break;
                case CborObjectFormat.IntKeyMap:
                    {
                        if (memberIndexCount != _memberMappings.Count)
                        {
                            throw new CborException($"expecting all fields/properties to get a member index in class/struct {ObjectType.Name}");
                        }

                        bool indexDuplicates = _memberMappings
                            .GroupBy(x => x.MemberIndex)
                            .Any(g => g.Count() > 1);

                        if (indexDuplicates)
                        {
                            throw new CborException($"class/struct {ObjectType.Name} holds duplicated MemberIndex fields/properties");
                        }

                        _memberMappings = _memberMappings
                            .OrderBy(m => m.MemberIndex)
                            .ToList();
                    }
                    break;
                case CborObjectFormat.Array:
                    {
                        if (memberIndexCount != _memberMappings.Count)
                        {
                            throw new CborException($"exepcting all fields/properties to get a member index in class/struct {ObjectType.Name}");
                        }

                        _memberMappings = _memberMappings
                            .OrderBy(m => m.MemberIndex)
                            .ToList();

                        for (int i = 0; i < _memberMappings.Count; i++)
                        {
                            if (_memberMappings[i].MemberIndex != i)
                            {
                                throw new CborException($"class/struct {ObjectType.Name} MemberIndexes must follow one another with no holes");
                            }
                        }
                    }
                    break;
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
