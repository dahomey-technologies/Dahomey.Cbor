using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Providers
{
    public class TupleConverterProvider : CborConverterProviderBase
    {
        private static readonly HashSet<Type> ValueTupleTypes = new HashSet<Type>(
        [
            typeof(ValueTuple<>),
            typeof(ValueTuple<,>),
            typeof(ValueTuple<,,>),
            typeof(ValueTuple<,,,>),
            typeof(ValueTuple<,,,,>),
            typeof(ValueTuple<,,,,,>),
            typeof(ValueTuple<,,,,,,>),
            typeof(ValueTuple<,,,,,,,>)
        ]);

        public override ICborConverter? GetConverter(Type type, CborOptions options)
        {
            if (type.IsGenericType && ValueTupleTypes.Contains(type.GetGenericTypeDefinition()))
            {
                FieldInfo[] fields = type.GetFields();
                switch (fields.Length)
                {
                    case 2:
                        return CreateGenericConverter(options, typeof(Tuple2Converter<,>), fields.Select(field => field.FieldType).ToArray());

                    case 3:
                        return CreateGenericConverter(options, typeof(Tuple3Converter<,,>), fields.Select(field => field.FieldType).ToArray());

                    case 4:
                        return CreateGenericConverter(options, typeof(Tuple4Converter<,,,>), fields.Select(field => field.FieldType).ToArray());

                    case 5:
                        return CreateGenericConverter(options, typeof(Tuple5Converter<,,,,>), fields.Select(field => field.FieldType).ToArray());

                    case 6:
                        return CreateGenericConverter(options, typeof(Tuple6Converter<,,,,,>), fields.Select(field => field.FieldType).ToArray());

                    case 7:
                        return CreateGenericConverter(options, typeof(Tuple7Converter<,,,,,,>), fields.Select(field => field.FieldType).ToArray());

                    case 8:
                        return CreateGenericConverter(options, typeof(Tuple8Converter<,,,,,,,>), fields.Select(field => field.FieldType).ToArray());
                    
                    default:
                        throw new CborException($"Tuples of length {fields.Length} are not supported");
                }
            }

            return null;
        }
    }
}