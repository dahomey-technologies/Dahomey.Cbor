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

                    // Eight fields is seven items and a Rest, whatever the tuple's real arity: the
                    // eighth type argument is the Rest's own type, and Tuple8Converter recurses into
                    // it. So this case covers every arity from eight upwards, and the field count
                    // never exceeds eight because that is how C# represents a tuple.
                    case 8:
                        return CreateGenericConverter(options, typeof(Tuple8Converter<,,,,,,,>), fields.Select(field => field.FieldType).ToArray());

                    // Only reachable as the Rest of an eight-element tuple: C# has no one-element
                    // tuple literal. It is a tuple all the same, and its converter is what the arity
                    // above delegates to.
                    case 1:
                        return CreateGenericConverter(options, typeof(Tuple1Converter<>), fields[0].FieldType);

                    default:
                        throw new CborException($"Tuples of length {fields.Length} are not supported");
                }
            }

            return null;
        }
    }
}