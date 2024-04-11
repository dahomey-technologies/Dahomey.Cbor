using Dahomey.Cbor.ObjectModel;
using System;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class PrimitiveConverterProvider : CborConverterProviderBase
    {
        public override ICborConverter? GetConverter(Type type, CborOptions options)
        {
            if (type.IsEnum)
            {
                return CreateGenericConverter(options, typeof(EnumConverter<>), type);
            }
            
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return new BooleanConverter();
                case TypeCode.Byte:
                    return new ByteConverter();
                case TypeCode.Char:
                    return new CharConverter();
                case TypeCode.DateTime:
                    return new DateTimeConverter(options);
                case TypeCode.Decimal:
                    return new DecimalConverter();
                case TypeCode.Double:
                    return new DoubleConverter();
                case TypeCode.Int16:
                    return new Int16Converter();
                case TypeCode.Int32:
                    return new Int32Converter();
                case TypeCode.Int64:
                    return new Int64Converter();
                case TypeCode.SByte:
                    return new SByteConverter();
                case TypeCode.Single:
                    return new SingleConverter();
                case TypeCode.String:
                    return new StringConverter();
                case TypeCode.UInt16:
                    return new UInt16Converter();
                case TypeCode.UInt32:
                    return new UInt32Converter();
                case TypeCode.UInt64:
                    return new UInt64Converter();
            }

            if (type == typeof(ReadOnlyMemory<byte>))
            {
                return new ReadOnlyMemoryConverter();
            }

            if (type == typeof(byte[]))
            {
                return new ByteArrayConverter();
            }

            if (typeof(CborValue).IsAssignableFrom(type))
            {
                return CreateConverter(options, typeof(CborValueConverter));
            }

            return null;
        }
    }
}
