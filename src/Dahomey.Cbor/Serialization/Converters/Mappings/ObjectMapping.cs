using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters;
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
        private readonly object _lock = new object();
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
            NamingConvention = options.DefaultNamingConvention;
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
                DiscriminatorMapping<T> memberMapping = new DiscriminatorMapping<T>(_options, this);
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
        /// Returns the member map for a member, creating and adding one if the mapping does not
        /// already cover that member.
        /// </summary>
        /// <remarks>
        /// A member the mapping already covers — which <see cref="AutoMap"/> is the usual way of
        /// arranging — is adjusted rather than mapped a second time, so
        /// <c>AutoMap().MapMember(o =&gt; o.A).SetRequired(...)</c> means what it reads as.
        /// </remarks>
        /// <typeparam name="TM">The member type.</typeparam>
        /// <param name="memberLambda">A lambda expression specifying the member.</param>
        /// <returns>The member map.</returns>
        public MemberMapping<T> MapMember<TM>(Expression<Func<T, TM>> memberLambda)
        {
            (MemberInfo memberInfo, Type memberType) = GetMemberInfoFromLambda(memberLambda);
            return MapMember(memberInfo, memberType);
        }

        /// <summary>
        /// Returns the member map for a member, creating and adding one if the mapping does not
        /// already cover that member.
        /// </summary>
        /// <param name="memberInfo">The field or property to map.</param>
        /// <param name="memberType">The type of that field or property.</param>
        /// <returns>The member map.</returns>
        public MemberMapping<T> MapMember(MemberInfo memberInfo, Type memberType)
        {
            // Adjusting one member is what this call is for: "take the conventions, then set
            // SetRequired/SetConverter/… on this one" is how AutoMap followed by MapMember reads, and
            // the only alternative is ClearMemberMappings() plus mapping every member by hand, which
            // drifts the moment a member is added to the type. Appending a second mapping for the
            // same member instead wrote it under two keys when the second call renamed it — a
            // well-formed document carrying a member it should not — and collided under #177's
            // duplicate-name check when it did not. Returning the mapping already covering the member
            // matches MongoDB.Bson's BsonClassMap.MapMember, whose shape this API takes.
            //
            // Only a MemberMapping<T> can be returned, so only those are looked at. Every mapping
            // this library builds from a member is one; an implementation of IMemberMapping from
            // outside it that names a member of its own is left to the duplicate-name check, which
            // reports the collision rather than this silently returning something that is not the
            // mapping the caller was handed.
            foreach (IMemberMapping memberMapping in _memberMappings)
            {
                if (memberMapping is MemberMapping<T> existingMapping
                    && IsSameMember(existingMapping.MemberInfo, memberInfo))
                {
                    return existingMapping;
                }
            }

            MemberMapping<T> newMapping = new MemberMapping<T>(_registry.ConverterRegistry, this, memberInfo, memberType);
            _memberMappings.Add(newMapping);
            return newMapping;
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
                DiscriminatorMapping<T> memberMapping = new DiscriminatorMapping<T>(_options, this);
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
        /// <remarks>
        /// In <see cref="CborObjectFormat.Array"/> the document carries no keys, so
        /// <c>CborPropertyAttribute.Index</c> orders the members rather than addressing them: they are
        /// written in ascending index order and read back by position. Gaps and negative indexes are
        /// allowed and do not reach the wire, so two types differing only in their index values encode
        /// identically. <see cref="CborObjectFormat.IntKeyMap"/> is the format whose index is written
        /// into the document and so survives as an address.
        /// </remarks>
        public void SetObjectFormat(CborObjectFormat objectFormat)
        {
            ObjectFormat = objectFormat;
        }
        
        public IReadOnlyCollection<IMemberMapping> GetMemberMappingsForConverter(ICborConverter converter)
        {
            EnsureInitialize(converter);
            return _memberMappings;
        }

        private void EnsureInitialize(ICborConverter? converter = null)
        {
            if (!_isInitialized)
            {
                lock (_lock)
                {
                    if (!_isInitialized)
                    {
                        _orderByAction?.Invoke();
                        ValidateMemberNamesAndindexes(converter);

                        _isInitialized = true;
                    }
                }
            }
        }

        private void ValidateMemberNamesAndindexes(ICborConverter? converter = null)
        {
            int memberNameCount = _memberMappings.Count(m =>
            {
                var memberName = converter is null ? m.MemberName : m.GetMemberNameForConverter(converter);
                return memberName != null;
            });
            int memberIndexCount = _memberMappings.Count(m => m.MemberIndex.HasValue);

            switch (ObjectFormat)
            {
                case CborObjectFormat.StringKeyMap:
                    {
                        if (memberNameCount != _memberMappings.Count)
                        {
                            throw new CborException($"expecting all fields/properties to get a member name in class/struct {ObjectType.Name}");
                        }

                        // Two members under one name is ambiguous in both directions: the write emits
                        // the key twice, producing a document this very type refuses to read back,
                        // and the read can only ever reach one of the two members. Refusing the
                        // mapping names the type and the name; letting it build names neither, and
                        // shows up as a duplicate key in a document nobody wrote by hand.
                        string? duplicatedName = _memberMappings
                            .GroupBy(m => converter is null ? m.MemberName : m.GetMemberNameForConverter(converter), StringComparer.Ordinal)
                            .FirstOrDefault(g => g.Count() > 1)?.Key;

                        if (duplicatedName != null)
                        {
                            throw new CborException(MemberMappingErrors.DuplicateMemberName(ObjectType, duplicatedName));
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
                            throw new CborException(MemberMappingErrors.DuplicateMemberIndex(ObjectType));
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

                        bool indexDuplicates = _memberMappings
                            .GroupBy(x => x.MemberIndex)
                            .Any(g => g.Count() > 1);

                        if (indexDuplicates)
                        {
                            throw new CborException(MemberMappingErrors.DuplicateMemberIndex(ObjectType));
                        }

                        _memberMappings = _memberMappings
                            .OrderBy(m => m.MemberIndex)
                            .ToList();
                    }
                    break;
            }
        }

        /// <summary>
        /// Whether two <see cref="MemberInfo"/> denote the same declared field or property.
        /// </summary>
        /// <remarks>
        /// Reference equality is not enough for a member declared on a base type: the conventions
        /// reflect over <typeparamref name="T"/>, so an inherited member arrives with
        /// <c>ReflectedType</c> set to <typeparamref name="T"/>, while <c>o =&gt; o.A</c> compiles to
        /// the accessor on the declaring type and so arrives with <c>ReflectedType</c> set to that
        /// base. The two are unequal, and the same declaration. Comparing the metadata definition —
        /// what <c>MemberInfo.HasSameMetadataDefinitionAs</c> does, which netstandard2.0 does not
        /// have — sees through the difference.
        /// </remarks>
        private static bool IsSameMember(MemberInfo? left, MemberInfo? right)
        {
            if (left is null || right is null)
            {
                // A null one is a mapping defined by delegates rather than by reflection - the
                // discriminator, or what a source generator emits - which no member identifies.
                return false;
            }

            return left == right
                || (left.MetadataToken == right.MetadataToken && left.Module.Equals(right.Module));
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
