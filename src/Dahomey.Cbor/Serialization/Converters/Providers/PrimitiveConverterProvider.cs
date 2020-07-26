using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class PrimitiveConverterProvider : CborConverterProviderBase
    {
        private static readonly Dictionary<Type, Type> _converterTypes = new Dictionary<Type, Type>
        {
            [typeof(bool)] = typeof(BooleanConverter),
            [typeof(sbyte)] = typeof(SByteConverter),
            [typeof(byte)] = typeof(ByteConverter),
            [typeof(short)] = typeof(Int16Converter),
            [typeof(ushort)] = typeof(UInt16Converter),
            [typeof(int)] = typeof(Int32Converter),
            [typeof(uint)] = typeof(UInt32Converter),
            [typeof(long)] = typeof(Int64Converter),
            [typeof(ulong)] = typeof(UInt64Converter),
            [typeof(float)] = typeof(SingleConverter),
            [typeof(double)] = typeof(DoubleConverter),
            [typeof(decimal)] = typeof(DecimalConverter),
            [typeof(string)] = typeof(StringConverter),
            [typeof(DateTime)] = typeof(DateTimeConverter),
            [typeof(ReadOnlyMemory<byte>)] = typeof(ReadOnlyMemoryConverter),
            [typeof(byte[])] = typeof(ByteArrayConverter),
        };

        public override ICborConverter? GetConverter(Type type, SerializationRegistry registry)
        {
            if (_converterTypes.TryGetValue(type, out Type? converterType))
            {
                return CreateConverter(registry, converterType);
            }

            if (type.IsEnum)
            {
                return CreateGenericConverter(registry, typeof(EnumConverter<>), type);
            }

            if (typeof(CborValue).IsAssignableFrom(type))
            {
                return CreateConverter(registry, typeof(CborValueConverter));
            }

            return null;
        }
    }
}
