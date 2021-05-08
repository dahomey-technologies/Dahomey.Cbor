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

    public interface IMemberConverter<T>
    {
        void Read(ref CborReader reader, ref T instance);
        void Write(ref CborWriter writer, ref T instance);
        bool ShouldSerialize(ref T instance, Type declaredType);
    }
    public class MemberConverter<T, TM> : IMemberConverter
    {
        private readonly Func<T, TM>? _memberGetter;
        private readonly Action<T, TM>? _memberSetter;
        private readonly ICborConverter<TM> _converter;
        private ReadOnlyMemory<byte> _memberName;
        private readonly TM _defaultValue;
        private readonly bool _ignoreIfDefault;
        private readonly Func<object, bool>? _shouldSerializeMethod;
        private readonly LengthMode _lengthMode;
        private readonly RequirementPolicy _requirementPolicy;
        private readonly bool _isClass = typeof(TM).IsClass;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;
        public bool IgnoreIfDefault => _ignoreIfDefault;
        public RequirementPolicy RequirementPolicy => _requirementPolicy;

        public MemberConverter(CborConverterRegistry registry, IMemberMapping memberMapping)
        {
            MemberInfo? memberInfo = memberMapping.MemberInfo;

            if (memberInfo == null)
            {
                throw new CborException("MemberInfo must not be null");
            }

            _memberName = Encoding.UTF8.GetBytes(memberMapping.MemberName!);
            _memberGetter = GenerateGetter(memberInfo);
            _memberSetter = GenerateSetter(memberInfo);
            _converter = (ICborConverter<TM>)memberMapping.Converter!;
            _defaultValue = (TM)memberMapping.DefaultValue!;
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

            if (_memberSetter == null)
            {
                throw new CborException($"No member setter for '{Encoding.UTF8.GetString(_memberName.Span)}'");
            }

            _memberSetter((T)obj, _converter.Read(ref reader));
        }

        public void Write(ref CborWriter writer, object obj)
        {
            if (_memberGetter == null)
            {
                throw new CborException($"No member getter for '{Encoding.UTF8.GetString(_memberName.Span)}'");
            }

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
            return _converter.Read(ref reader)!;
        }

        public void Set(object obj, object value)
        {
            if (_memberSetter == null)
            {
                throw new CborException($"No member setter for '{Encoding.UTF8.GetString(_memberName.Span)}'");
            }

            _memberSetter((T)obj, (TM)value);
        }

        public bool ShouldSerialize(object obj, Type declaredType, CborOptions options)
        {
            if (_memberGetter == null)
            {
                throw new CborException($"No member getter for '{Encoding.UTF8.GetString(_memberName.Span)}'");
            }

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

        private Func<T, TM>? GenerateGetter(MemberInfo memberInfo)
        {
            switch(memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (propertyInfo.GetMethod != null && propertyInfo.GetMethod.IsStatic)
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

        private Action<T, TM>? GenerateSetter(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (!propertyInfo.CanWrite || (propertyInfo.SetMethod != null && propertyInfo.SetMethod.IsStatic))
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

    public class StructMemberConverter<T, TM> : IMemberConverter, IMemberConverter<T>
        where T : struct
    {
        private readonly StructMemberGetterDelegate<T, TM>? _memberGetter;
        private readonly StructMemberSetterDelegate<T, TM>? _memberSetter;
        private readonly ICborConverter<TM> _converter;
        private readonly ReadOnlyMemory<byte> _memberName;
        private readonly TM _defaultValue;
        private readonly bool _ignoreIfDefault;
        private readonly RequirementPolicy _requirementPolicy;
        private readonly bool _isClass = typeof(TM).IsClass;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;
        public string MemberNameAsString { get; }
        public bool IgnoreIfDefault => _ignoreIfDefault;
        public RequirementPolicy RequirementPolicy => _requirementPolicy;

        public StructMemberConverter(CborConverterRegistry registry, IMemberMapping memberMapping)
        {
            MemberInfo? memberInfo = memberMapping.MemberInfo;

            if (memberInfo == null)
            {
                throw new CborException("MemberInfo must not be null");
            }

            MemberNameAsString = memberMapping.MemberName!;
            _memberName = Encoding.UTF8.GetBytes(MemberNameAsString);
            _memberGetter = GenerateGetter(memberInfo);
            _memberSetter = GenerateSetter(memberInfo);
            _converter = (ICborConverter<TM>)memberMapping.Converter!;
            _defaultValue = (TM)memberMapping.DefaultValue!;
            _ignoreIfDefault = memberMapping.IgnoreIfDefault;
            _requirementPolicy = memberMapping.RequirementPolicy;
        }

        public void Read(ref CborReader reader, object obj)
        {
            throw new NotSupportedException();
        }

        public void Write(ref CborWriter writer, object obj)
        {
            throw new NotSupportedException();
        }

        public object Read(ref CborReader reader)
        {
            return _converter.Read(ref reader)!;
        }

        public void Set(object obj, object value)
        {
            throw new NotSupportedException();
        }

        public bool ShouldSerialize(object obj, Type declaredType, CborOptions options)
        {
            throw new NotSupportedException();
        }

        public void Read(ref CborReader reader, ref T instance)
        {
            if (reader.GetCurrentDataItemType() == CborDataItemType.Null)
            {
                if (_requirementPolicy == RequirementPolicy.DisallowNull || _requirementPolicy == RequirementPolicy.Always)
                {
                    throw new CborException($"Property '{MemberNameAsString}' cannot be null.");
                }
            }

            if (_memberSetter == null)
            {
                throw new CborException($"No member setter for '{MemberNameAsString}'");
            }

            _memberSetter(ref instance, _converter.Read(ref reader));
        }

        public void Write(ref CborWriter writer, ref T instance)
        {
            if (_memberGetter == null)
            {
                throw new CborException($"No member getter for '{MemberNameAsString}'");
            }

            TM value = _memberGetter(ref instance);

            if (_isClass && value == null && (_requirementPolicy == RequirementPolicy.DisallowNull
                || _requirementPolicy == RequirementPolicy.Always))
            {
                throw new CborException($"Property '{MemberNameAsString}' cannot be null.");
            }

            _converter.Write(ref writer, value);
        }

        public bool ShouldSerialize(ref T instance, Type declaredType)
        {
            if (_memberGetter == null)
            {
                throw new CborException($"No member getter for '{MemberNameAsString}'");
            }

            if (IgnoreIfDefault && EqualityComparer<TM>.Default.Equals(_memberGetter(ref instance), _defaultValue))
            {
                return false;
            }

            return true;
        }

        private StructMemberGetterDelegate<T, TM>? GenerateGetter(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (propertyInfo.GetMethod != null && propertyInfo.GetMethod.IsStatic)
                    {
                        if (!propertyInfo.CanRead)
                        {
                            return null;
                        }

                        ParameterExpression instanceParam = Expression.Parameter(typeof(T), "instance");
                        return Expression.Lambda<StructMemberGetterDelegate<T, TM>>(
                            Expression.Property(null, propertyInfo),
                            instanceParam).Compile();
                    }

                    return propertyInfo.CanRead
                       ? propertyInfo.GenerateStructGetter<T, TM>()
                       : null;

                case FieldInfo fieldInfo:
                    return fieldInfo.GenerateStructGetter<T, TM>();

                default:
                    return null;
            }
        }

        private StructMemberSetterDelegate<T, TM>? GenerateSetter(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    if (!propertyInfo.CanWrite || propertyInfo.SetMethod != null && propertyInfo.SetMethod.IsStatic)
                    {
                        return null;
                    }

                    return propertyInfo.GenerateStructSetter<T, TM>();

                case FieldInfo fieldInfo:
                    if (fieldInfo.IsStatic || fieldInfo.IsInitOnly)
                    {
                        return null;
                    }

                    return fieldInfo.GenerateStructSetter<T, TM>();

                default:
                    return null;
            }
        }
    }

    public class DiscriminatorMemberConverter<T> : IMemberConverter
    {
        private readonly IDiscriminatorConvention _discriminatorConvention;
        private readonly CborDiscriminatorPolicy _discriminatorPolicy;
        private readonly ReadOnlyMemory<byte> _memberName;

        public DiscriminatorMemberConverter(
            IDiscriminatorConvention discriminatorConvention, 
            CborDiscriminatorPolicy discriminatorPolicy)
        {
            _discriminatorConvention = discriminatorConvention;
            _discriminatorPolicy = discriminatorPolicy;

            if (discriminatorConvention != null)
            {
                _memberName = discriminatorConvention.MemberName.ToArray();
            }
        }

        public ReadOnlySpan<byte> MemberName => _memberName.Span;
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
            if (_discriminatorConvention == null)
            {
                return false;
            }

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
