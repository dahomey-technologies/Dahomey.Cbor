using Dahomey.Cbor.Attributes;
using System;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class MemberMapping<T> : IMemberMapping where T : class
    {
        private readonly IObjectMapping _objectMapping;
        private readonly CborConverterRegistry _converterRegistry;

        public MemberInfo MemberInfo { get; private set; }
        public Type MemberType { get; private set; }
        public string MemberName { get; private set; }
        public ICborConverter Converter { get; private set; }
        public bool CanBeDeserialized { get; private set; }
        public bool CanBeSerialized { get; private set; }
        public object DefaultValue { get; private set; }
        public bool IgnoreIfDefault { get; private set; }
        public Func<object, bool> ShouldSerializeMethod { get; private set; }
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
            MemberName = memberName;
            return this;
        }

        public MemberMapping<T> SetConverter(ICborConverter converter)
        {
            Converter = converter;
            return this;
        }

        public MemberMapping<T> SetDefaultValue(object defaultValue)
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

        public void Initialize()
        {
            InitializeMemberName();
            InitializeConverter();
            InitializeCanBeDeserialized();
            InitializeCanBeSerialized();
            ValidateDefaultValue();
        }

        public void PostInitialize()
        {
        }

        public IMemberConverter GenerateMemberConverter()
        {
            IMemberConverter memberConverter = (IMemberConverter)Activator.CreateInstance(
                typeof(MemberConverter<,>).MakeGenericType(typeof(T), MemberType),
                _converterRegistry, this);

            return memberConverter;
        }

        private void InitializeMemberName()
        {
            if (string.IsNullOrEmpty(MemberName))
            {
                CborPropertyAttribute cborPropertyAttribute = MemberInfo.GetCustomAttribute<CborPropertyAttribute>();
                if (cborPropertyAttribute != null && cborPropertyAttribute.PropertyName != null)
                {
                    MemberName = cborPropertyAttribute.PropertyName;
                }
                else if (_objectMapping.NamingConvention != null)
                {
                    MemberName = _objectMapping.NamingConvention.GetPropertyName(MemberInfo.Name);
                }
                else
                {
                    MemberName = MemberInfo.Name;
                }
            }
        }

        private void InitializeConverter()
        {
            if (Converter == null)
            {
                CborConverterAttribute converterAttribute = MemberInfo.GetCustomAttribute<CborConverterAttribute>();
                if (converterAttribute != null)
                {
                    Type converterType = converterAttribute.ConverterType;
                    VerifyMemberConverterType(converterType);

                    Converter = (ICborConverter)Activator.CreateInstance(converterType);
                }
                else
                {
                    Converter = _converterRegistry.Lookup(MemberType);
                }
            }
            else
            {
                VerifyMemberConverterType(Converter.GetType());
            }
        }

        private void InitializeCanBeDeserialized()
        {
            switch (MemberInfo)
            {
                case PropertyInfo propertyInfo:
                    CanBeDeserialized = propertyInfo.CanWrite && !propertyInfo.GetMethod.IsStatic;
                    break;

                case FieldInfo fieldInfo:
                    CanBeDeserialized = !fieldInfo.IsInitOnly && !fieldInfo.IsStatic;
                    break;

                default:
                    CanBeDeserialized = false;
                    break;
            }
        }

        private void InitializeCanBeSerialized()
        {
            switch (MemberInfo)
            {
                case PropertyInfo propertyInfo:
                    CanBeSerialized = propertyInfo.CanRead;
                    break;

                case FieldInfo fieldInfo:
                    CanBeSerialized = true;
                    break;

                default:
                    CanBeSerialized = false;
                    break;
            }
        }

        private void VerifyMemberConverterType(Type memberConverterType)
        {
            Type interfaceType = typeof(ICborConverter<>).MakeGenericType(MemberType);
            if (!memberConverterType.GetInterfaces().Any(i => i == interfaceType))
            {
                throw new CborException($"Custom converter on member {MemberInfo.ReflectedType.Name}.{MemberInfo.Name} is not a ICborConverter<{MemberType.Name}>");
            }
        }

        private void ValidateDefaultValue()
        {
            if ((DefaultValue == null && MemberType.IsValueType)
                || (DefaultValue != null && DefaultValue.GetType() != MemberType))
            {
                throw new CborException($"Default value type mismatch");
            }
        }
    }
}
