using Dahomey.Cbor.Attributes;
using System;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class MemberMapping : IMemberMapping
    {
        private readonly IObjectMapping _objectMapping;
        private readonly CborConverterRegistry _converterRegistry;

        public MemberInfo MemberInfo { get; private set; }
        public Type MemberType { get; private set; }
        public string MemberName { get; private set; }
        public ICborConverter MemberConverter { get; private set; }
        public bool CanBeDeserialized { get; private set; }
        public bool CanBeSerialized { get; private set; }
        public object DefaultValue { get; private set; }
        public bool IgnoreIfDefault { get; private set; }

        public MemberMapping(CborConverterRegistry converterRegistry,
            IObjectMapping objectMapping, MemberInfo memberInfo, Type memberType)
        {
            _objectMapping = objectMapping;
            _converterRegistry = converterRegistry;
            MemberInfo = memberInfo;
            MemberType = memberType;
            DefaultValue = (memberType.IsClass || memberType.IsInterface) ? null : Activator.CreateInstance(memberType);
        }

        public MemberMapping SetMemberName(string memberName)
        {
            MemberName = memberName;
            return this;
        }

        public MemberMapping SetMemberConverter(ICborConverter converter)
        {
            MemberConverter = converter;
            return this;
        }

        public MemberMapping SetDefaultValue(object defaultValue)
        {
            DefaultValue = defaultValue;
            return this;
        }

        public MemberMapping SetIngoreIfDefault(bool ignoreIfDefault)
        {
            IgnoreIfDefault = ignoreIfDefault;
            return this;
        }

        public void Initialize()
        {
            InitializeMemberName();
            InitializeMemberConverter();
            InitializeCanBeDeserialized();
            InitializeCanBeSerialized();
            ValidateDefaultValue();
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

        private void InitializeMemberConverter()
        {
            if (MemberConverter == null)
            {
                CborConverterAttribute converterAttribute = MemberInfo.GetCustomAttribute<CborConverterAttribute>();
                if (converterAttribute != null)
                {
                    Type converterType = converterAttribute.ConverterType;
                    VerifyMemberConverterType(converterType);

                    MemberConverter = (ICborConverter)Activator.CreateInstance(converterType);
                }
                else
                {
                    MemberConverter = _converterRegistry.Lookup(MemberType);
                }
            }
            else
            {
                VerifyMemberConverterType(MemberConverter.GetType());
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
