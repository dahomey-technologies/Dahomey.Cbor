using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class PrimitiveConverterProvider : CborConverterProviderBase
    {
        private static readonly Dictionary<Type, Lazy<ICborConverter>> _lazyConverterTypes = new()
        {
            [typeof(bool)] = new(() => new BooleanConverter()),
            [typeof(sbyte)] = new(() => new SByteConverter()),
            [typeof(byte)] = new(() => new ByteConverter()),
            [typeof(short)] = new(() => new Int16Converter()),
            [typeof(ushort)] = new(() => new UInt16Converter()),
            [typeof(int)] = new(() => new Int32Converter()),
            [typeof(uint)] = new(() => new UInt32Converter()),
            [typeof(long)] = new(() => new Int64Converter()),
            [typeof(ulong)] = new(() => new UInt64Converter()),
            [typeof(float)] = new(() => new SingleConverter()),
            [typeof(double)] = new(() => new DoubleConverter()),
            [typeof(decimal)] = new(() => new DecimalConverter()),
            [typeof(string)] = new(() => new StringConverter()),
            [typeof(ReadOnlyMemory<byte>)] = new(() => new ReadOnlyMemoryConverter()),
            [typeof(byte[])] = new(() => new ByteArrayConverter()),
        };

        public override ICborConverter? GetConverter(Type type, CborOptions options)
        {
            if (_lazyConverterTypes.TryGetValue(type, out var converterType))
            {
                return converterType.Value;
            }

            if (type == typeof(DateTime))
            {
                return new DateTimeConverter(options);
            }

            if (type.IsEnum)
            {
                return CreateGenericConverter(options, typeof(EnumConverter<>), type);
            }

            if (typeof(CborValue).IsAssignableFrom(type))
            {
                return CreateConverter(options, typeof(CborValueConverter));
            }

            return null;
        }
    }
}
