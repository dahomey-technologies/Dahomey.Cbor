using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IMemberConverter
    {
        ReadOnlySpan<byte> MemberName { get; }
        bool IgnoreIfDefault { get; }
        RequirementPolicy RequirementPolicy { get; }

        void Read(ref CborReader reader, object obj);
        void Write(ref CborWriter writer, object obj);
        object Read(ref CborReader reader);
        void Set(object obj, object value);
        bool ShouldSerialize(object obj, Type declaredType, CborOptions options);
    }

    public class MemberConverter<T, TM> : IMemberConverter
        where T : class
    {
        private readonly Func<T, TM> _memberGetter;
        private readonly Action<T, TM> _memberSetter;
        private readonly ICborConverter<TM> _converter;
        private ReadOnlyMemory<byte> _memberName;
        private readonly TM _defaultValue;
        private readonly bool _ignoreIfDefault;
        private readonly Func<object, bool> _shouldSerializeMethod;
        private readonly LengthMode _lengthMode;
        private readonly RequirementPolicy _requirementPolicy;
        private readonly bool _isClass = typeof(TM).IsClass;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;
        public bool IgnoreIfDefault => _ignoreIfDefault;
        public RequirementPolicy RequirementPolicy => _requirementPolicy;

        public MemberConverter(CborConverterRegistry registry, IMemberMapping memberMapping)
        {
            MemberInfo memberInfo = memberMapping.MemberInfo;

            _memberName = Encoding.UTF8.GetBytes(memberMapping.MemberName);
            _memberGetter = GenerateGetter(memberInfo);
            _memberSetter = GenerateSetter(memberInfo);
            _converter = (ICborConverter<TM>)memberMapping.Converter;
            _defaultValue = (TM)memberMapping.DefaultValue;
            _ignoreIfDefault = memberMapping.IgnoreIfDefault;
            _shouldSerializeMethod = memberMapping.ShouldSerializeMethod;
            _lengthMode = memberMapping.LengthMode;
            _requirementPolicy = memberMapping.RequirementPolicy;
        }

        public void Read(ref CborReader reader, object obj)
        {
            if (reader.GetCurrentDataItemType() == CborDataItemType.Null)
            {
                if (_requirementPolicy == RequirementPolicy.DisallowNull || _requirementPolicy == RequirementPolicy.Always)
                {
                    throw new CborException($"Property '{Encoding.UTF8.GetString(_memberName.Span)}' cannot be null.");
                }
            }

            _memberSetter((T)obj, _converter.Read(ref reader));
        }

        public void Write(ref CborWriter writer, object obj)
        {
            TM value = _memberGetter((T)obj);

            if (_isClass && value == null && (_requirementPolicy == RequirementPolicy.DisallowNull
                || _requirementPolicy == RequirementPolicy.Always))
            {
                throw new CborException($"Property '{Encoding.UTF8.GetString(_memberName.Span)}' cannot be null.");
            }

            _converter.Write(ref writer, value, _lengthMode);
        }

        public object Read(ref CborReader reader)
        {
            return _converter.Read(ref reader);
        }

        public void Set(object obj, object value)
        {
            _memberSetter((T)obj, (TM)value);
        }

        public bool ShouldSerialize(object obj, Type declaredType, CborOptions options)
        {
            if (IgnoreIfDefault && EqualityComparer<TM>.Default.Equals(_memberGetter((T)obj), _defaultValue))
            {
                return false;
            }

            if (_shouldSerializeMethod != null && !_shouldSerializeMethod(obj))
            {
                return false;
            }

            return true;
        }

        private Func<T, TM> GenerateGetter(MemberInfo memberInfo)
        {
            switch(memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (propertyInfo.GetMethod.IsStatic)
                    {
                        if (!propertyInfo.CanRead)
                        {
                            return null;
                        }

                        ParameterExpression objParam = Expression.Parameter(typeof(T), "obj");
                        return Expression.Lambda<Func<T, TM>>(
                            Expression.Property(null, propertyInfo),
                            objParam).Compile();
                    }

                    return propertyInfo.CanRead
                       ? propertyInfo.GenerateGetter<T, TM>()
                       : null;

                case FieldInfo fieldInfo:
                    return fieldInfo.GenerateGetter<T, TM>();

                default:
                    return null;
            }
        }

        private Action<T, TM> GenerateSetter(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (!propertyInfo.CanWrite || propertyInfo.SetMethod.IsStatic)
                    {
                        return null;
                    }

                    return propertyInfo.GenerateSetter<T, TM>();

                case FieldInfo fieldInfo:
                    if (fieldInfo.IsStatic || fieldInfo.IsInitOnly)
                    {
                        return null;
                    }

                    return fieldInfo.GenerateSetter<T, TM>();

                default:
                    return null;
            }
        }
    }

    public class DiscriminatorMemberConverter<T> : IMemberConverter
        where T : class
    {
        private readonly IDiscriminatorConvention _discriminatorConvention;
        private readonly CborDiscriminatorPolicy _discriminatorPolicy;

        public DiscriminatorMemberConverter(
            IDiscriminatorConvention discriminatorConvention, 
            CborDiscriminatorPolicy discriminatorPolicy)
        {
            _discriminatorConvention = discriminatorConvention;
            _discriminatorPolicy = discriminatorPolicy;
        }

        public ReadOnlySpan<byte> MemberName => _discriminatorConvention.MemberName;
        public bool IgnoreIfDefault => false;
        public RequirementPolicy RequirementPolicy => RequirementPolicy.Never;

        public void Read(ref CborReader reader, object obj)
        {
            throw new NotSupportedException();
        }

        public object Read(ref CborReader reader)
        {
            throw new NotSupportedException();
        }

        public void Set(object obj, object value)
        {
            throw new NotSupportedException();
        }

        public bool ShouldSerialize(object obj, Type declaredType, CborOptions options)
        {
            CborDiscriminatorPolicy discriminatorPolicy = _discriminatorPolicy != CborDiscriminatorPolicy.Default ? _discriminatorPolicy
                : (options.DiscriminatorPolicy != CborDiscriminatorPolicy.Default ? options.DiscriminatorPolicy : CborDiscriminatorPolicy.Auto);

            return discriminatorPolicy == CborDiscriminatorPolicy.Always
                || discriminatorPolicy == CborDiscriminatorPolicy.Auto && obj.GetType() != declaredType;
        }

        public void Write(ref CborWriter writer, object obj)
        {
            _discriminatorConvention.WriteDiscriminator(ref writer, obj.GetType());
        }
    }
}
