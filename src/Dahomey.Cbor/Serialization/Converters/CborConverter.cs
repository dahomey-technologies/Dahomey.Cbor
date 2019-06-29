using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters
{
    public interface ICborConverter
    {
    }

    public interface ICborValueReader<T>
    {
        T Read(ref CborReader reader);
    }

    public interface ICborValueWriter<T>
    {
        void Write(ref CborWriter writer, T value);
    }

    public interface ICborConverter<T> : ICborConverter, ICborValueReader<T>, ICborValueWriter<T>
    {
    }

    public static class CborConverter
    {
        private static readonly ConcurrentDictionary<Type, ICborConverter> converters = new ConcurrentDictionary<Type, ICborConverter>();

        private static class Cache<T>
        {
            public static ICborConverter<T> Converter = (ICborConverter<T>)Lookup(typeof(T));
        }

        public static ICborConverter<T> Lookup<T>()
        {
            return Cache<T>.Converter;
        }

        public static ICborConverter Lookup(Type type)
        {
            return converters.GetOrAdd(type, Instantiate);
        }

        public static bool Register(Type type, ICborConverter converter)
        {
            return converters.TryAdd(type, converter);
        }

        private static ICborConverter Instantiate(Type type)
        {
            CborConverterAttribute converterAttribute = type.GetCustomAttribute<CborConverterAttribute>();
            if (converterAttribute != null)
            {
                return (ICborConverter)Activator.CreateInstance(converterAttribute.ConverterType);
            }

            if (type == typeof(bool))
            {
                return new BooleanConverter();
            }
            if (type == typeof(sbyte))
            {
                return new SByteConverter();
            }
            if (type == typeof(byte))
            {
                return new ByteConverter();
            }
            if (type == typeof(short))
            {
                return new Int16Converter();
            }
            if (type == typeof(ushort))
            {
                return new UInt16Converter();
            }
            if (type == typeof(int))
            {
                return new Int32Converter();
            }
            if (type == typeof(uint))
            {
                return new UInt32Converter();
            }
            if (type == typeof(long))
            {
                return new Int64Converter();
            }
            if (type == typeof(ulong))
            {
                return new UInt64Converter();
            }
            if (type == typeof(float))
            {
                return new SingleConverter();
            }
            if (type == typeof(double))
            {
                return new DoubleConverter();
            }
            if (type == typeof(string))
            {
                return new StringConverter();
            }
            if (type == typeof(DateTime))
            {
                return new DateTimeConverter();
            }
            if (type.IsEnum)
            {
                return (ICborConverter)Activator.CreateInstance(typeof(EnumConverter<>).MakeGenericType(type));
            }
            if (type == typeof(CborValue))
            {
                return new CborValueConverter();
            }
            if (type.IsArray)
            {
                Type itemType = type.GetElementType();
                return (ICborConverter)Activator.CreateInstance(typeof(ArrayConverter<>).MakeGenericType(itemType));
            }
            if (type.IsGenericType)
            {
                if (type.GetInterfaces()
                    .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                {
                    Type keyType = type.GetGenericArguments()[0];
                    Type valueType = type.GetGenericArguments()[1];
                    return (ICborConverter)Activator.CreateInstance(typeof(DictionaryConverter<,>).MakeGenericType(keyType, valueType));
                }
                if (type.GetInterfaces()
                    .Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>)))
                {
                    Type itemType = type.GetGenericArguments()[0];
                    return (ICborConverter)Activator.CreateInstance(typeof(CollectionConverter<,>).MakeGenericType(type, itemType));
                }
            }
            if (type.IsClass)
            {
                return (ICborConverter)Activator.CreateInstance(typeof(ObjectConverter<>).MakeGenericType(type));
            }

            throw new NotSupportedException(string.Format("type {0} not supported", type));
        }
    }
}
