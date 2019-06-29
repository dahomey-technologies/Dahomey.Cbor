using Dahomey.Cbor.Attributes;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface IMemberConverter
    {
        void Read(ref CborReader reader, object obj);
        void Write(ref CborWriter writer, object obj);
        ReadOnlySpan<byte> MemberName { get; }
    }

    public abstract class MemberConverter<T, TP> : IMemberConverter
        where T : class, new()
    {
        private readonly Func<T, TP> _memberGetter;
        private readonly Action<T, TP> _memberSetter;
        private readonly ICborConverter<TP> _memberConverter;
        private ReadOnlyMemory<byte> _memberName;

        public ReadOnlySpan<byte> MemberName => _memberName.Span;

        public MemberConverter(MemberInfo memberInfo, ObjectConverter<T> objectConverter)
        {
            _memberName = GetMemberName(memberInfo, objectConverter);

            _memberGetter = GenerateGetter(memberInfo);
            _memberSetter = GenerateSetter(memberInfo);

            CborConverterAttribute converterAttribute = memberInfo.GetCustomAttribute<CborConverterAttribute>();
            if (converterAttribute != null)
            {
                object converter = Activator.CreateInstance(converterAttribute.ConverterType);
                if (!(converter is ICborConverter<TP> memberConverter))
                {
                    throw new CborException($"Custom converter on member {memberInfo.ReflectedType.Name}.{memberInfo.Name} is not a ICborConverter<{converterAttribute.ConverterType.Name}>");
                }
                _memberConverter = memberConverter;
            }
            else
            {
                _memberConverter = CborConverter.Lookup<TP>();
            }
        }

        public void Read(ref CborReader reader, object obj)
        {
            _memberSetter((T)obj, _memberConverter.Read(ref reader));
        }

        public void Write(ref CborWriter writer, object obj)
        {
            _memberConverter.Write(ref writer, _memberGetter((T)obj));
        }

        protected abstract Func<T, TP> GenerateGetter(MemberInfo memberInfo);
        protected abstract Action<T, TP> GenerateSetter(MemberInfo memberInfo);

        private ReadOnlyMemory<byte> GetMemberName(MemberInfo memberInfo, ObjectConverter<T> objectConverter)
        {
            CborPropertyAttribute cborPropertyAttribute = memberInfo.GetCustomAttribute<CborPropertyAttribute>();
            if (cborPropertyAttribute != null)
            {
                return Encoding.UTF8.GetBytes(cborPropertyAttribute.PropertyName);
            }
            else if (objectConverter.NamingConvention != null)
            {
                return objectConverter.NamingConvention.GetPropertyName(memberInfo.Name);
            }
            else
            {
                return Encoding.UTF8.GetBytes(memberInfo.Name);
            }
        }
    }

    public class PropertyConverter<T, TP> : MemberConverter<T, TP>
        where T : class, new()
    {
        public PropertyConverter(PropertyInfo propertyInfo, ObjectConverter<T> objectConverter)
            : base(propertyInfo, objectConverter)
        {
        }

        protected override Func<T, TP> GenerateGetter(MemberInfo memberInfo)
        {
            return (Func<T, TP>)((PropertyInfo)memberInfo).GetMethod.CreateDelegate(typeof(Func<T, TP>));
        }

        protected override Action<T, TP> GenerateSetter(MemberInfo memberInfo)
        {
            return (Action<T, TP>)((PropertyInfo)memberInfo).SetMethod.CreateDelegate(typeof(Action<T, TP>));
        }
    }

    public class FieldConverter<T, TP> : MemberConverter<T, TP>
        where T : class, new()
    {
        public FieldConverter(FieldInfo fieldInfo, ObjectConverter<T> objectConverter)
            : base(fieldInfo, objectConverter)
        {
        }

        protected override Func<T, TP> GenerateGetter(MemberInfo memberInfo)
        {
            ParameterExpression objParam = Expression.Parameter(typeof(T), "obj");

            return Expression.Lambda<Func<T, TP>>(
                Expression.Field(objParam, (FieldInfo)memberInfo),
                objParam) .Compile();
        }

        protected override Action<T, TP> GenerateSetter(MemberInfo memberInfo)
        {
            ParameterExpression objParam = Expression.Parameter(typeof(T), "obj");
            ParameterExpression valueParam = Expression.Parameter(typeof(TP), "value");

            return Expression.Lambda<Action<T, TP>>(
                Expression.Assign(
                    Expression.Field(
                        objParam, 
                        (FieldInfo)memberInfo),
                    valueParam),
                objParam, valueParam).Compile();
        }
    }
}
