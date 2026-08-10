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
    }

    public class Tuple2Converter<T1, T2> : CborConverterBase<(T1, T2)>
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

        public override (T1, T2) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 2)
            {
                throw new CborException("Expected CBOR Array of size 2");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 2");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 2");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            return (item1, item2);
        }

        public override void Write(ref CborWriter writer, (T1, T2) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 2;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple3Converter<T1, T2, T3> : CborConverterBase<(T1, T2, T3)>
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

        public override (T1, T2, T3) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 3)
            {
                throw new CborException("Expected CBOR Array of size 3");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 3");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 3");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 3");
            }
            T3 item3 = TupleItemReader.Read(ref reader, _item3Converter, 2);

            return (item1, item2, item3);
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 3;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple4Converter<T1, T2, T3, T4> : CborConverterBase<(T1, T2, T3, T4)>
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

        public override (T1, T2, T3, T4) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 4)
            {
                throw new CborException("Expected CBOR Array of size 4");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 4");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 4");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 4");
            }
            T3 item3 = TupleItemReader.Read(ref reader, _item3Converter, 2);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 4");
            }
            T4 item4 = TupleItemReader.Read(ref reader, _item4Converter, 3);

            return (item1, item2, item3, item4);
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 4;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple5Converter<T1, T2, T3, T4, T5> : CborConverterBase<(T1, T2, T3, T4, T5)>
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

        public override (T1, T2, T3, T4, T5) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 5)
            {
                throw new CborException("Expected CBOR Array of size 5");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 5");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 5");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 5");
            }
            T3 item3 = TupleItemReader.Read(ref reader, _item3Converter, 2);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 5");
            }
            T4 item4 = TupleItemReader.Read(ref reader, _item4Converter, 3);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 5");
            }
            T5 item5 = TupleItemReader.Read(ref reader, _item5Converter, 4);

            return (item1, item2, item3, item4, item5);
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 5;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple6Converter<T1, T2, T3, T4, T5, T6> : CborConverterBase<(T1, T2, T3, T4, T5, T6)>
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

        public override (T1, T2, T3, T4, T5, T6) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 6)
            {
                throw new CborException("Expected CBOR Array of size 6");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 6");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 6");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 6");
            }
            T3 item3 = TupleItemReader.Read(ref reader, _item3Converter, 2);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 6");
            }
            T4 item4 = TupleItemReader.Read(ref reader, _item4Converter, 3);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 6");
            }
            T5 item5 = TupleItemReader.Read(ref reader, _item5Converter, 4);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 6");
            }
            T6 item6 = TupleItemReader.Read(ref reader, _item6Converter, 5);

            return (item1, item2, item3, item4, item5, item6);
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5, T6) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 6;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            _item6Converter.Write(ref writer, value.Item6);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple7Converter<T1, T2, T3, T4, T5, T6, T7> : CborConverterBase<(T1, T2, T3, T4, T5, T6, T7)>
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

        public override (T1, T2, T3, T4, T5, T6, T7) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 7)
            {
                throw new CborException("Expected CBOR Array of size 7");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T3 item3 = TupleItemReader.Read(ref reader, _item3Converter, 2);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T4 item4 = TupleItemReader.Read(ref reader, _item4Converter, 3);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T5 item5 = TupleItemReader.Read(ref reader, _item5Converter, 4);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T6 item6 = TupleItemReader.Read(ref reader, _item6Converter, 5);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 7");
            }
            T7 item7 = TupleItemReader.Read(ref reader, _item7Converter, 6);

            return (item1, item2, item3, item4, item5, item6, item7);
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5, T6, T7) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 7;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            _item6Converter.Write(ref writer, value.Item6);
            _item7Converter.Write(ref writer, value.Item7);
            writer.WriteEndArray(size);
        }
    }

    public class Tuple8Converter<T1, T2, T3, T4, T5, T6, T7, T8> : CborConverterBase<(T1, T2, T3, T4, T5, T6, T7, T8)>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<T1> _item1Converter;
        private readonly ICborConverter<T2> _item2Converter;
        private readonly ICborConverter<T3> _item3Converter;
        private readonly ICborConverter<T4> _item4Converter;
        private readonly ICborConverter<T5> _item5Converter;
        private readonly ICborConverter<T6> _item6Converter;
        private readonly ICborConverter<T7> _item7Converter;
        private readonly ICborConverter<T8> _item8Converter;

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
            _item8Converter = options.Registry.ConverterRegistry.Lookup<T8>();
        }
        public override (T1, T2, T3, T4, T5, T6, T7, T8) Read(ref CborReader reader)
        {
            // ReadSize does not skip a semantic tag, and this is the only converter that reaches
            // the reader below its tag-skipping entry points. Skip them here so a tagged tuple
            // stays readable; the tags themselves carry no information this converter needs.
            // A loop rather than one skip: since #183 SkipSemanticTag consumes a whole stack, and
            // stopping at the first would leave this the one converter that does not - a tag 4
            // decimal fraction under an outer tag reads as a two-element array everywhere else.
            while (reader.TryReadSemanticTag(out _))
            {
            }

            int size = reader.ReadSize();

            if (size != -1 && size != 8)
            {
                throw new CborException("Expected CBOR Array of size 8");
            }

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T1 item1 = TupleItemReader.Read(ref reader, _item1Converter, 0);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T2 item2 = TupleItemReader.Read(ref reader, _item2Converter, 1);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T3 item3 = TupleItemReader.Read(ref reader, _item3Converter, 2);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T4 item4 = TupleItemReader.Read(ref reader, _item4Converter, 3);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T5 item5 = TupleItemReader.Read(ref reader, _item5Converter, 4);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T6 item6 = TupleItemReader.Read(ref reader, _item6Converter, 5);

            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T7 item7 = TupleItemReader.Read(ref reader, _item7Converter, 6);


            if (size == -1 && reader.IsBreak())
            {
                throw new CborException("Expected CBOR Array of size 8");
            }
            T8 item8 = TupleItemReader.Read(ref reader, _item8Converter, 7);

            return (item1, item2, item3, item4, item5, item6, item7, item8);
        }

        public override void Write(ref CborWriter writer, (T1, T2, T3, T4, T5, T6, T7, T8) value, LengthMode lengthMode)
        {
            lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode;
            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : 8;

            writer.WriteBeginArray(size);
            _item1Converter.Write(ref writer, value.Item1);
            _item2Converter.Write(ref writer, value.Item2);
            _item3Converter.Write(ref writer, value.Item3);
            _item4Converter.Write(ref writer, value.Item4);
            _item5Converter.Write(ref writer, value.Item5);
            _item6Converter.Write(ref writer, value.Item6);
            _item7Converter.Write(ref writer, value.Item7);
            _item8Converter.Write(ref writer, value.Item8);
            writer.WriteEndArray(size);
        }
    }
}