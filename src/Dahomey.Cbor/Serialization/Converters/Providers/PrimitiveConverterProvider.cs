using Dahomey.Cbor.ObjectModel;
using System;
using System.Numerics;

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
                    return new DecimalConverter(options);
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

            // Not a TypeCode, so it cannot join the switch above.
            //
            // MATCHED PAIR: the four scalars below are duplicated by name in
            // src/Dahomey.Cbor.Generator/TypeCollector.cs (IsPrimitive) and rendered by name in
            // CddlTypeReference.RenderPrimitive. They cannot be shared -- the generator is an analyzer
            // assembly and must not reference this one -- so a converter added or removed here has to
            // be mirrored there. PrimitiveConverterProviderParityTests is what catches a one-sided
            // edit; without a mirror, a generated context maps the type as an object and writes
            // different bytes from this path, with no diagnostic.
            if (type == typeof(Half))
            {
                return new HalfConverter();
            }

            if (type == typeof(BigInteger))
            {
                return new BigIntegerConverter();
            }

            if (type == typeof(CborDecimalFraction))
            {
                return new DecimalFractionConverter();
            }

            if (type == typeof(CborBigFloat))
            {
                return new BigFloatConverter();
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
