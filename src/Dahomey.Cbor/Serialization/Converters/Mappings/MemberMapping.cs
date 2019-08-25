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
        private string _memberName;
        private ICborConverter _memberConverter;
        private bool? _canBeDeserialized;
        private bool? _canBeSerialized;

        public MemberInfo MemberInfo { get; private set; }
        public Type MemberType { get; private set; }
        public string MemberName => GetMemberName();
        public ICborConverter MemberConverter => GetMemberConverter();
        public bool CanBeDeserialized => GetCanBeDeserialized();
        public bool CanBeSerialized => GetCanBeSerialized();
        public object DefaultValue { get; }

        public MemberMapping(CborConverterRegistry converterRegistry,
            IObjectMapping objectMapping, MemberInfo memberInfo, Type memberType)
        {
            _objectMapping = objectMapping;
            _converterRegistry = converterRegistry;
            MemberInfo = memberInfo;
            MemberType = memberType;
            DefaultValue = memberType.IsClass ? null : Activator.CreateInstance(memberType);
        }

        public MemberMapping SetMemberName(string memberName)
        {
            _memberName = memberName;
            return this;
        }

        public MemberMapping SetMemberConverter(ICborConverter converter)
        {
            VerifyMemberConverterType(converter.GetType());
            _memberConverter = converter;
            return this;
        }

        private string GetMemberName()
        {
            if (string.IsNullOrEmpty(_memberName))
            {
                CborPropertyAttribute cborPropertyAttribute = MemberInfo.GetCustomAttribute<CborPropertyAttribute>();
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

            return _memberName;
        }

        private ICborConverter GetMemberConverter()
        {
            if (_memberConverter == null)
            {
                CborConverterAttribute converterAttribute = MemberInfo.GetCustomAttribute<CborConverterAttribute>();
                if (converterAttribute != null)
                {
                    Type converterType = converterAttribute.ConverterType;
                    VerifyMemberConverterType(converterType);

                    _memberConverter = (ICborConverter)Activator.CreateInstance(converterType);
                }
                else
                {
                    _memberConverter = _converterRegistry.Lookup(MemberType);
                }
            }

            return _memberConverter;
        }

        private bool GetCanBeDeserialized()
        {
            if (!_canBeDeserialized.HasValue)
            {
                switch (MemberInfo)
                {
                    case PropertyInfo propertyInfo:
                        _canBeDeserialized = propertyInfo.CanWrite && !propertyInfo.GetMethod.IsStatic;
                        break;

                    case FieldInfo fieldInfo:
                        _canBeDeserialized = !fieldInfo.IsInitOnly && !fieldInfo.IsStatic;
                        break;

                    default:
                        _canBeDeserialized = false;
                        break;
                }
            }

            return _canBeDeserialized.Value;
        }

        private bool GetCanBeSerialized()
        {
            if (!_canBeSerialized.HasValue)
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

            return _canBeSerialized.Value;
        }

        private void VerifyMemberConverterType(Type memberConverterType)
        {
            Type interfaceType = typeof(ICborConverter<>).MakeGenericType(MemberType);
            if (!memberConverterType.GetInterfaces().Any(i => i == interfaceType))
            {
                throw new CborException($"Custom converter on member {MemberInfo.ReflectedType.Name}.{MemberInfo.Name} is not a ICborConverter<{MemberType.Name}>");
            }
        }
    }
}
