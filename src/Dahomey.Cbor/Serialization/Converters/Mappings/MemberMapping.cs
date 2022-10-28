using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Util;
using System;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class MemberMapping<T> : IMemberMapping
    {
        private bool _isInitialized = false;
        private readonly IObjectMapping _objectMapping;
        private readonly CborConverterRegistry _converterRegistry;
        private string? _memberName = null;
        private ICborConverter? _converter = null;
        private bool _canBeDeserialized = false;
        private bool _canBeSerialized = false;

        public MemberInfo MemberInfo { get; private set; }
        public Type MemberType { get; private set; }
        public string? MemberName
        {
            get
            {
                EnsureInitialize();
                return _memberName;
            }
        }

        public ICborConverter? Converter
        {
            get
            {
                EnsureInitialize();
                return _converter;
            }
        }

        public bool CanBeDeserialized
        {
            get
            {
                EnsureInitialize();
                return _canBeDeserialized;
            }
        }

        public bool CanBeSerialized
        {
            get
            {
                EnsureInitialize();
                return _canBeSerialized;
            }
        }

        public object? DefaultValue { get; private set; }
        public bool IgnoreIfDefault { get; private set; }
        public Func<object, bool>? ShouldSerializeMethod { get; private set; }
        public LengthMode LengthMode { get; private set; }
        public RequirementPolicy RequirementPolicy { get; private set; }

        public MemberMapping(CborConverterRegistry converterRegistry,
            IObjectMapping objectMapping, MemberInfo memberInfo, Type memberType)
        {
            _objectMapping = objectMapping;
            _converterRegistry = converterRegistry;
            MemberInfo = memberInfo;
            MemberType = memberType;
            DefaultValue = (memberType.IsClass || memberType.IsInterface) ? null : Activator.CreateInstance(memberType);
        }

        public MemberMapping<T> SetMemberName(string memberName)
        {
            _memberName = memberName;
            return this;
        }

        public MemberMapping<T> SetConverter(ICborConverter converter)
        {
            _converter = converter;
            return this;
        }

        public MemberMapping<T> SetDefaultValue(object? defaultValue)
        {
            DefaultValue = defaultValue;
            return this;
        }

        public MemberMapping<T> SetIngoreIfDefault(bool ignoreIfDefault)
        {
            IgnoreIfDefault = ignoreIfDefault;
            return this;
        }

        public MemberMapping<T> SetShouldSerializeMethod(Func<object, bool> shouldSerializeMethod)
        {
            ShouldSerializeMethod = shouldSerializeMethod;
            return this;
        }

        public MemberMapping<T> SetLengthMode(LengthMode lengthMode)
        {
            LengthMode = lengthMode;
            return this;
        }

        public MemberMapping<T> SetRequired(RequirementPolicy requirementPolicy)
        {
            RequirementPolicy = requirementPolicy;
            return this;
        }

        public void EnsureInitialize()
        {
            if (!_isInitialized)
            {
                lock (this)
                {
                    if (!_isInitialized)
                    {
                        InitializeMemberName();
                        InitializeConverter();
                        InitializeCanBeDeserialized();
                        InitializeCanBeSerialized();
                        ValidateDefaultValue();

                        _isInitialized = true;
                    }
                }
            }
        }

        public IMemberConverter GenerateMemberConverter()
        {
            Type type;

            if (typeof(T).IsStruct())
            {
                type = typeof(StructMemberConverter<,>).MakeGenericType(typeof(T), MemberType);
            }
            else
            {
                type = typeof(MemberConverter<,>).MakeGenericType(typeof(T), MemberType);
            }

            IMemberConverter? memberConverter = 
                (IMemberConverter?)Activator.CreateInstance(type, _converterRegistry, this);

            if (memberConverter == null)
            {
                throw new CborException($"Cannot instantiate {type}");
            }

            return memberConverter;
        }

        private void InitializeMemberName()
        {
            if (string.IsNullOrEmpty(_memberName))
            {
                CborPropertyAttribute? cborPropertyAttribute = MemberInfo.GetCustomAttribute<CborPropertyAttribute>();
                if (cborPropertyAttribute != null && cborPropertyAttribute.PropertyName != null)
                {
                    _memberName = cborPropertyAttribute.PropertyName;
                }
                else if (_objectMapping.NamingConvention != null)
                {
                    _memberName = _objectMapping.NamingConvention.GetPropertyName(MemberInfo.Name);
                }
                else
                {
                    _memberName = MemberInfo.Name;
                }
            }
        }

        private void InitializeConverter()
        {
            if (_converter == null)
            {
                CborConverterAttribute? converterAttribute = MemberInfo.GetCustomAttribute<CborConverterAttribute>();
                if (converterAttribute != null)
                {
                    Type converterType = converterAttribute.ConverterType;
                    VerifyMemberConverterType(converterType);

                    _converter = (ICborConverter?)Activator.CreateInstance(converterType);

                    if (_converter == null)
                    {
                        throw new CborException($"Cannot instantiate {converterType}");
                    }
                }
                else
                {
                    _converter = _converterRegistry.Lookup(MemberType);
                }
            }
            else
            {
                VerifyMemberConverterType(_converter.GetType());
            }
        }

        private void InitializeCanBeDeserialized()
        {
            switch (MemberInfo)
            {
                case PropertyInfo propertyInfo:
                    _canBeDeserialized = propertyInfo.CanWrite && propertyInfo.GetMethod != null && !propertyInfo.GetMethod.IsStatic;
                    break;

                case FieldInfo fieldInfo:
                    _canBeDeserialized = !fieldInfo.IsInitOnly && !fieldInfo.IsStatic;
                    break;

                default:
                    _canBeDeserialized = false;
                    break;
            }
        }

        private void InitializeCanBeSerialized()
        {
            switch (MemberInfo)
            {
                case PropertyInfo propertyInfo:
                    _canBeSerialized = propertyInfo.CanRead;
                    break;

                case FieldInfo fieldInfo:
                    _canBeSerialized = true;
                    break;

                default:
                    _canBeSerialized = false;
                    break;
            }
        }

        private void VerifyMemberConverterType(Type memberConverterType)
        {
            Type interfaceType = typeof(ICborConverter<>).MakeGenericType(MemberType);
            if (!memberConverterType.GetInterfaces().Any(i => i == interfaceType))
            {
                throw new CborException($"Custom converter on member {MemberInfo.ReflectedType?.Name}.{MemberInfo.Name} is not a ICborConverter<{MemberType.Name}>");
            }
        }

        private void ValidateDefaultValue()
        {
            if ((DefaultValue == null && MemberType.IsValueType && Nullable.GetUnderlyingType(MemberType) == null)
                || (DefaultValue != null && DefaultValue.GetType() != MemberType))
            {
                throw new CborException($"Default value type mismatch");
            }
        }
    }
}
