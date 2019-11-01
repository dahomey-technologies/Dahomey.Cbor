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
        void Read(ref CborReader reader, object obj);
        void Write(ref CborWriter writer, object obj);
        object Read(ref CborReader reader);
        void Set(object obj, object value);
        bool ShouldSerialize(object obj);
    }

    public class MemberConverter<T, TM> : IMemberConverter
        where T : class
    {
        private readonly Func<T, TM> _memberGetter;
        private readonly Action<T, TM> _memberSetter;
        private readonly ICborConverter<TM> _memberConverter;
        private ReadOnlyMemory<byte> _memberName;
        private readonly TM _defaultValue;
        private readonly bool _ignoreIfDefault;
        private readonly Func<object, bool> _shouldSeriliazeMethod;
        private readonly LengthMode _lengthMode;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;
        public bool IgnoreIfDefault => _ignoreIfDefault;

        public MemberConverter(CborConverterRegistry registry, IMemberMapping memberMapping)
        {
            MemberInfo memberInfo = memberMapping.MemberInfo;

            _memberName = Encoding.UTF8.GetBytes(memberMapping.MemberName);
            _memberGetter = GenerateGetter(memberInfo);
            _memberSetter = GenerateSetter(memberInfo);
            _memberConverter = (ICborConverter<TM>)memberMapping.MemberConverter;
            _defaultValue = (TM)memberMapping.DefaultValue;
            _ignoreIfDefault = memberMapping.IgnoreIfDefault;
            _shouldSeriliazeMethod = memberMapping.ShouldSerializeMethod;
            _lengthMode = memberMapping.LengthMode;
        }

        public void Read(ref CborReader reader, object obj)
        {
            _memberSetter((T)obj, _memberConverter.Read(ref reader));
        }

        public void Write(ref CborWriter writer, object obj)
        {
            _memberConverter.Write(ref writer, _memberGetter((T)obj), _lengthMode);
        }

        public object Read(ref CborReader reader)
        {
            return _memberConverter.Read(ref reader);
        }

        public void Set(object obj, object value)
        {
            _memberSetter((T)obj, (TM)value);
        }

        public bool ShouldSerialize(object obj)
        {
            if (IgnoreIfDefault && EqualityComparer<TM>.Default.Equals(_memberGetter((T)obj), _defaultValue))
            {
                return false;
            }

            if (_shouldSeriliazeMethod != null && !_shouldSeriliazeMethod(obj))
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

        public DiscriminatorMemberConverter(IDiscriminatorConvention discriminatorConvention)
        {
            _discriminatorConvention = discriminatorConvention;
        }

        public ReadOnlySpan<byte> MemberName => _discriminatorConvention.MemberName;

        public bool IgnoreIfDefault => false;

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

        public bool ShouldSerialize(object obj)
        {
            return true;
        }

        public void Write(ref CborWriter writer, object obj)
        {
            _discriminatorConvention.WriteDiscriminator<T>(ref writer, obj.GetType());
        }
    }
}
