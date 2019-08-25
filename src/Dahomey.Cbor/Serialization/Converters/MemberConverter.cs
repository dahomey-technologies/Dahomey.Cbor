using Dahomey.Cbor.Serialization.Converters.Mappings;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IMemberConverter
    {
        ReadOnlySpan<byte> MemberName { get; }
        void Read(ref CborReader reader, object obj);
        void Write(ref CborWriter writer, object obj);
        object Read(ref CborReader reader);
        void Set(object obj, object value);
    }

    public class MemberConverter<T, TP> : IMemberConverter
        where T : class
    {
        private readonly IMemberMapping _memberMapping;
        private readonly Func<T, TP> _memberGetter;
        private readonly Action<T, TP> _memberSetter;
        private readonly ICborConverter<TP> _memberConverter;
        private ReadOnlyMemory<byte> _memberName;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public MemberConverter(CborConverterRegistry registry, IMemberMapping memberMapping)
        {
            _memberMapping = memberMapping;

            MemberInfo memberInfo = _memberMapping.MemberInfo;

            _memberName = Encoding.UTF8.GetBytes(_memberMapping.MemberName);
            _memberGetter = GenerateGetter(memberInfo);
            _memberSetter = GenerateSetter(memberInfo);
            _memberConverter = (ICborConverter<TP>)_memberMapping.MemberConverter;
        }

        public void Read(ref CborReader reader, object obj)
        {
            _memberSetter((T)obj, _memberConverter.Read(ref reader));
        }

        public void Write(ref CborWriter writer, object obj)
        {
            _memberConverter.Write(ref writer, _memberGetter((T)obj));
        }

        public object Read(ref CborReader reader)
        {
            return _memberConverter.Read(ref reader);
        }

        public void Set(object obj, object value)
        {
            _memberSetter((T)obj, (TP)value);
        }

        private Func<T, TP> GenerateGetter(MemberInfo memberInfo)
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
                        return Expression.Lambda<Func<T, TP>>(
                            Expression.Property(null, propertyInfo),
                            objParam).Compile();
                    }

                    return propertyInfo.CanRead
                       ? (Func<T, TP>)propertyInfo.GetMethod.CreateDelegate(typeof(Func<T, TP>))
                       : null;

                case FieldInfo fieldInfo:
                    {
                        ParameterExpression objParam = Expression.Parameter(typeof(T), "obj");
                        return Expression.Lambda<Func<T, TP>>(
                            Expression.Field(fieldInfo.IsStatic ? null : objParam, fieldInfo),
                            objParam).Compile();
                    }

                default:
                    return null;
            }
        }

        private Action<T, TP> GenerateSetter(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo propertyInfo:
                    return (propertyInfo.CanWrite && !propertyInfo.SetMethod.IsStatic)
                       ? (Action<T, TP>)propertyInfo.SetMethod.CreateDelegate(typeof(Action<T, TP>))
                       : null;

                case FieldInfo fieldInfo:
                    if (fieldInfo.IsStatic || fieldInfo.IsInitOnly)
                    {
                        return null;
                    }

                    ParameterExpression objParam = Expression.Parameter(typeof(T), "obj");
                    ParameterExpression valueParam = Expression.Parameter(typeof(TP), "value");

                    return Expression.Lambda<Action<T, TP>>(
                        Expression.Assign(
                            Expression.Field(
                                objParam,
                                fieldInfo),
                            valueParam),
                        objParam, valueParam).Compile();

                default:
                    return null;
            }
        }
    }
}
