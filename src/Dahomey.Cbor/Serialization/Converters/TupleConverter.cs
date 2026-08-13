using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads one item of a tuple, naming its position in <see cref="CborException.Path"/> if it fails.
    /// </summary>
    /// <remarks>
    /// A tuple has no member names to report, so the position is all there is to say - and it is what a
    /// caller needs, because the items of a tuple are otherwise indistinguishable in a message. Shared
    /// by every arity rather than written out in each, since each item read is otherwise identical.
    /// </remarks>
    internal static class TupleItemReader
    {
        public static T Read<T>(ref CborReader reader, ICborConverter<T> converter, int index)
        {
            try
            {
                return converter.Read(ref reader);
            }
            catch (CborException exception)
            {
                exception.PrependPathIndex(index);
                throw;
            }
        }

        /// <summary>
        /// Reads one item at <paramref name="index"/> of a tuple of <paramref name="arity"/> items,
        /// advancing the index. The break check before it is what catches an indefinite-length array
        /// that ends early.
        /// </summary>
        public static T ReadItem<T>(
            ref CborReader reader, ICborConverter<T> converter, int size, int arity, ref int index)
        {
            if (size == -1 && reader.IsBreak())
            {
                throw new CborException($"Expected CBOR Array of size {arity}");
            }

            T value = Read(ref reader, converter, index);
            index++;

            return value;
        }
    }

    /// <summary>
    /// A tuple's items, written into an array someone else opened and read from one someone else
    /// entered.
    /// </summary>
    /// <remarks>
    /// This is what keeps an arity past seven flat on the wire. C# represents such a tuple as seven
    /// fields plus a <c>Rest</c> holding the overflow, and that nesting is an implementation detail of
    /// the language: a nine-element tuple is nine items, <c>[1, …, 9]</c>, not <c>[1, …, 7, [8, 9]]</c>.
    /// So the converter for the overflow contributes its items to the enclosing array rather than
    /// opening one, and <see cref="ItemCount"/> is the flattened arity that array is sized by.
    /// <para>
    /// Recursion rather than a class per arity: the nesting repeats every seven elements without limit,
    /// so a converter that delegates to its <c>Rest</c>'s items handles every arity there is with the
    /// eight classes already here.
    /// </para>
    /// </remarks>
    internal interface ICborTupleItems<T>
    {
        /// <summary>Items this contributes, counting everything nested inside a <c>Rest</c>.</summary>
        int ItemCount { get; }

        void WriteItems(ref CborWriter writer, T value);

        T ReadItems(ref CborReader reader, int size, int arity, ref int index);
    }

    /// <summary>
    /// A one-element tuple. Not reachable as a C# tuple literal, and needed because an eight-element
    /// tuple's <c>Rest</c> is exactly this.
    /// </summary>
    public class Tuple1Converter<T1> : CborConverterBase<ValueTuple<T1>>, ICborTupleItems<ValueTuple<T1>>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;

        public Tuple1Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
        }

        public int ItemCount => 1;

        public override ValueTuple<T1> Read(ref CborReader reader)
        {
            return TupleReader.Read<ValueTuple<T1>>(ref reader, this);
        }

        public ValueTuple<T1> ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return new ValueTuple<T1>(
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, ValueTuple<T1> value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, ValueTuple<T1> value)
        {
            _item1Converter.Write(ref writer, value.Item1);
        }
    }

    /// <summary>
    /// The array around a tuple's items: the part every arity shares, so no arity states it twice.
    /// </summary>
    internal static class TupleReader
    {
        public static T Read<T>(ref CborReader reader, ICborTupleItems<T> items)
        {
            // ReadBeginArray rather than going straight to ReadSize. ReadSize takes the additional
            // value off whatever header is current without asking which major type it belongs to, so
            // bytes(3), text(3), map(3) and even the bare unsigned integer 3 all yielded an arity of
            // 3 and decoded as a tuple; ReadBeginArray is where every other array reader gets that
            // check. It also steps over the whole stack of semantic tags, which ReadSize does not
            // and which this converter needs, being the only one that reaches the reader below its
            // tag-skipping entry points - a tagged tuple stays readable, and a tag 4 decimal
            // fraction under an outer tag reads as a two-element array here as it does everywhere
            // else.
            reader.ReadBeginArray();

            int arity = items.ItemCount;
            int size = reader.ReadSize();

            if (size != -1 && size != arity)
            {
                throw new CborException($"Expected CBOR Array of size {arity}");
            }

            int index = 0;
            T value = items.ReadItems(ref reader, size, arity, ref index);

            if (size == -1)
            {
                if (!reader.IsBreak())
                {
                    throw new CborException($"Expected CBOR Array of size {arity}");
                }

                reader.ConsumeBreak();
            }

            return value;
        }
    }

    internal static class TupleWriter
    {
        public static void Write<T>(
            ref CborWriter writer, ICborTupleItems<T> items, T value, LengthMode lengthMode, CborOptions options)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : items.ItemCount;

            writer.WriteBeginArray(size);
            items.WriteItems(ref writer, value);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple2Converter<T1, T2> : CborConverterBase<(T1, T2)>, ICborTupleItems<(T1, T2)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;

        public Tuple2Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
        }

        public int ItemCount => 2;

        public override (T1, T2) Read(ref CborReader reader)
        {
            return TupleReader.Read<(T1, T2)>(ref reader, this);
        }

        public (T1, T2) ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return (
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, (T1, T2) value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, (T1, T2) value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
        }
    }

    public class Tuple3Converter<T1, T2, T3> : CborConverterBase<(T1, T2, T3)>, ICborTupleItems<(T1, T2, T3)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;

        public Tuple3Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
            _item3Converter = options.Registry.ConverterRegistry.Lookup<T3>();
        }

        public int ItemCount => 3;

        public override (T1, T2, T3) Read(ref CborReader reader)
        {
            return TupleReader.Read<(T1, T2, T3)>(ref reader, this);
        }

        public (T1, T2, T3) ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return (
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item3Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3) value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, (T1, T2, T3) value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
        }
    }

    public class Tuple4Converter<T1, T2, T3, T4> : CborConverterBase<(T1, T2, T3, T4)>, ICborTupleItems<(T1, T2, T3, T4)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;
        private readonly ICborConverter<T4> _item4Converter;

        public Tuple4Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
            _item3Converter = options.Registry.ConverterRegistry.Lookup<T3>();
            _item4Converter = options.Registry.ConverterRegistry.Lookup<T4>();
        }

        public int ItemCount => 4;

        public override (T1, T2, T3, T4) Read(ref CborReader reader)
        {
            return TupleReader.Read<(T1, T2, T3, T4)>(ref reader, this);
        }

        public (T1, T2, T3, T4) ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return (
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item3Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item4Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4) value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, (T1, T2, T3, T4) value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
        }
    }

    public class Tuple5Converter<T1, T2, T3, T4, T5> : CborConverterBase<(T1, T2, T3, T4, T5)>, ICborTupleItems<(T1, T2, T3, T4, T5)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;
        private readonly ICborConverter<T4> _item4Converter;
        private readonly ICborConverter<T5> _item5Converter;

        public Tuple5Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
            _item3Converter = options.Registry.ConverterRegistry.Lookup<T3>();
            _item4Converter = options.Registry.ConverterRegistry.Lookup<T4>();
            _item5Converter = options.Registry.ConverterRegistry.Lookup<T5>();
        }

        public int ItemCount => 5;

        public override (T1, T2, T3, T4, T5) Read(ref CborReader reader)
        {
            return TupleReader.Read<(T1, T2, T3, T4, T5)>(ref reader, this);
        }

        public (T1, T2, T3, T4, T5) ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return (
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item3Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item4Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item5Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5) value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, (T1, T2, T3, T4, T5) value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
        }
    }

    public class Tuple6Converter<T1, T2, T3, T4, T5, T6> : CborConverterBase<(T1, T2, T3, T4, T5, T6)>, ICborTupleItems<(T1, T2, T3, T4, T5, T6)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;
        private readonly ICborConverter<T4> _item4Converter;
        private readonly ICborConverter<T5> _item5Converter;
        private readonly ICborConverter<T6> _item6Converter;

        public Tuple6Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
            _item3Converter = options.Registry.ConverterRegistry.Lookup<T3>();
            _item4Converter = options.Registry.ConverterRegistry.Lookup<T4>();
            _item5Converter = options.Registry.ConverterRegistry.Lookup<T5>();
            _item6Converter = options.Registry.ConverterRegistry.Lookup<T6>();
        }

        public int ItemCount => 6;

        public override (T1, T2, T3, T4, T5, T6) Read(ref CborReader reader)
        {
            return TupleReader.Read<(T1, T2, T3, T4, T5, T6)>(ref reader, this);
        }

        public (T1, T2, T3, T4, T5, T6) ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return (
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item3Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item4Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item5Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item6Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5, T6) value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, (T1, T2, T3, T4, T5, T6) value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            _item6Converter.Write(ref writer, value.Item6);
        }
    }

    public class Tuple7Converter<T1, T2, T3, T4, T5, T6, T7> : CborConverterBase<(T1, T2, T3, T4, T5, T6, T7)>, ICborTupleItems<(T1, T2, T3, T4, T5, T6, T7)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;
        private readonly ICborConverter<T4> _item4Converter;
        private readonly ICborConverter<T5> _item5Converter;
        private readonly ICborConverter<T6> _item6Converter;
        private readonly ICborConverter<T7> _item7Converter;

        public Tuple7Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
            _item3Converter = options.Registry.ConverterRegistry.Lookup<T3>();
            _item4Converter = options.Registry.ConverterRegistry.Lookup<T4>();
            _item5Converter = options.Registry.ConverterRegistry.Lookup<T5>();
            _item6Converter = options.Registry.ConverterRegistry.Lookup<T6>();
            _item7Converter = options.Registry.ConverterRegistry.Lookup<T7>();
        }

        public int ItemCount => 7;

        public override (T1, T2, T3, T4, T5, T6, T7) Read(ref CborReader reader)
        {
            return TupleReader.Read<(T1, T2, T3, T4, T5, T6, T7)>(ref reader, this);
        }

        public (T1, T2, T3, T4, T5, T6, T7) ReadItems(ref CborReader reader, int size, int arity, ref int index)
        {
            return (
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item3Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item4Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item5Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item6Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item7Converter, size, arity, ref index));
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5, T6, T7) value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, (T1, T2, T3, T4, T5, T6, T7) value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            _item6Converter.Write(ref writer, value.Item6);
            _item7Converter.Write(ref writer, value.Item7);
        }
    }

    /// <summary>
    /// Seven items and a <c>Rest</c> holding the overflow: how C# represents every tuple of more than
    /// seven elements, at every arity, since a <c>Rest</c> of more than seven nests again.
    /// </summary>
    /// <remarks>
    /// The base type is spelled <c>ValueTuple&lt;...&gt;</c> rather than <c>(T1, ..., TRest)</c>
    /// deliberately. The tuple syntax would expand to
    /// <c>ValueTuple&lt;T1, ..., T7, ValueTuple&lt;TRest&gt;&gt;</c> -- one level deeper than the type
    /// being converted -- which is exactly how this class came to be unreachable: the provider handed it
    /// the <c>Rest</c> field's type as an eighth element, so the converter built was for a different
    /// closed type than the one asked for, and the cast to it failed.
    /// <para>
    /// Delegating to the <c>Rest</c>'s own items is what makes every arity work with these eight
    /// classes and keeps the encoding flat: a nine-element tuple is <c>[1, ..., 9]</c>.
    /// </para>
    /// </remarks>
    public class Tuple8Converter<T1, T2, T3, T4, T5, T6, T7, TRest>
        : CborConverterBase<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>,
          ICborTupleItems<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>
        where TRest : struct
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;
        private readonly ICborConverter<T4> _item4Converter;
        private readonly ICborConverter<T5> _item5Converter;
        private readonly ICborConverter<T6> _item6Converter;
        private readonly ICborConverter<T7> _item7Converter;
        private readonly ICborTupleItems<TRest> _restItems;

        public Tuple8Converter(CborOptions options)
        {
            _options = options;
            _item1Converter = options.Registry.ConverterRegistry.Lookup<T1>();
            _item2Converter = options.Registry.ConverterRegistry.Lookup<T2>();
            _item3Converter = options.Registry.ConverterRegistry.Lookup<T3>();
            _item4Converter = options.Registry.ConverterRegistry.Lookup<T4>();
            _item5Converter = options.Registry.ConverterRegistry.Lookup<T5>();
            _item6Converter = options.Registry.ConverterRegistry.Lookup<T6>();
            _item7Converter = options.Registry.ConverterRegistry.Lookup<T7>();

            // The overflow is a tuple in its own right, and its converter contributes items to the array
            // this one opens rather than opening another. Asking the registry and then testing for the
            // internal interface is the shape AbstractCollectionConverter uses to find a typed array's
            // reader, for the same reason: the constraint the interface needs cannot be stated here.
            _restItems = options.Registry.ConverterRegistry.Lookup<TRest>() as ICborTupleItems<TRest>
                ?? throw new CborException(
                    $"A tuple's Rest must itself be a tuple, and {typeof(TRest)} is not");
        }

        public int ItemCount => 7 + _restItems.ItemCount;

        public override ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> Read(ref CborReader reader)
        {
            return TupleReader.Read<ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>>(ref reader, this);
        }

        public ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> ReadItems(
            ref CborReader reader, int size, int arity, ref int index)
        {
            return new ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest>(
                TupleItemReader.ReadItem(ref reader, _item1Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item2Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item3Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item4Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item5Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item6Converter, size, arity, ref index),
                TupleItemReader.ReadItem(ref reader, _item7Converter, size, arity, ref index),
                _restItems.ReadItems(ref reader, size, arity, ref index));
        }

        public override void Write(
            ref CborWriter writer, ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value, LengthMode lengthMode)
        {
            TupleWriter.Write(ref writer, this, value, lengthMode, _options);
        }

        public void WriteItems(ref CborWriter writer, ValueTuple<T1, T2, T3, T4, T5, T6, T7, TRest> value)
        {
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            _item6Converter.Write(ref writer, value.Item6);
            _item7Converter.Write(ref writer, value.Item7);
            _restItems.WriteItems(ref writer, value.Rest);
        }
    }
}
