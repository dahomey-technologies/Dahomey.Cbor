using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CborValueConverter :
        CborConverterBase<CborValue>,
        ICborConverter<CborObject?>,
        ICborConverter<CborArray?>,
        ICborMapReader<CborValueConverter.MapReaderContext>,
        ICborMapWriter<CborValueConverter.MapWriterContext>,
        ICborArrayReader<CborValueConverter.ArrayReaderContext>,
        ICborArrayWriter<CborValueConverter.ArrayWriterContext>
    {
        public struct MapReaderContext
        {
            public CborObject obj;
        }

        public struct MapWriterContext
        {
            public CborObject obj;
            public IEnumerator<KeyValuePair<CborValue, CborValue>> enumerator;
            public LengthMode lengthMode;
        }

        public struct ArrayReaderContext
        {
            public CborArray array;
        }

        public struct ArrayWriterContext
        {
            public CborArray array;
            public int index;
            public LengthMode lengthMode;
        }

        private readonly CborOptions _options;

        public CborValueConverter(CborOptions options)
        {
            _options = options;
        }

        public override CborValue Read(ref CborReader reader)
        {
            bool hasSemanticTag = reader.TryReadSemanticTag(out ulong semanticTag);

            var cborValue = ReadCborValue(ref reader);
            if (hasSemanticTag)
            {
                cborValue.SemanticTag = semanticTag;
            }

            return cborValue;
        }

        private CborValue ReadCborValue(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Boolean:
                    return reader.ReadBoolean();

                case CborDataItemType.Null:
                    reader.ReadNull();
                    return CborValue.Null;

                case CborDataItemType.Signed:
                    return reader.ReadInt64();

                case CborDataItemType.Unsigned:
                    return reader.ReadUInt64();

                case CborDataItemType.Single:
                    return reader.ReadSingle();

                case CborDataItemType.Double:
                    return reader.ReadDouble();

                case CborDataItemType.Decimal:
                    return reader.ReadDecimal();

                case CborDataItemType.String:
                    return reader.ReadString();

                case CborDataItemType.Array:
                    return ((ICborConverter<CborArray>)this).Read(ref reader);

                case CborDataItemType.Map:
                    return ((ICborConverter<CborObject>)this).Read(ref reader);

                case CborDataItemType.ByteString:
                    return reader.ReadByteString();

                default:
                    throw reader.BuildException("Unexpected data item type");
            }
        }

        public override void Write(ref CborWriter writer, CborValue value, LengthMode lengthMode)
        {
            switch (value.Type)
            {
                case CborValueType.Object:
                    ((ICborConverter<CborObject>)this).Write(ref writer, (CborObject)value, lengthMode);
                    break;

                case CborValueType.Array:
                    ((ICborConverter<CborArray>)this).Write(ref writer, (CborArray)value, lengthMode);
                    break;

                case CborValueType.Positive:
                    writer.WriteUInt64(value.Value<ulong>());
                    break;

                case CborValueType.Negative:
                    writer.WriteInt64(value.Value<long>());
                    break;

                case CborValueType.Single:
                    writer.WriteSingle(value.Value<float>());
                    break;

                case CborValueType.Double:
                    writer.WriteDouble(value.Value<double>());
                    break;

                case CborValueType.Decimal:
                    writer.WriteDecimal(value.Value<decimal>());
                    break;

                case CborValueType.String:
                    writer.WriteString(value.Value<string>());
                    break;

                case CborValueType.Boolean:
                    writer.WriteBoolean(value.Value<bool>());
                    break;

                case CborValueType.Null:
                    writer.WriteNull();
                    break;

                case CborValueType.ByteString:
                    writer.WriteByteString(value.Value<ReadOnlyMemory<byte>>().Span);
                    break;
            }
        }

        void ICborMapReader<MapReaderContext>.ReadBeginMap(int size, ref MapReaderContext context)
        {
            context.obj = new CborObject();
        }

        void ICborMapReader<MapReaderContext>.ReadMapItem(ref CborReader reader, ref MapReaderContext context)
        {
            CborValue key = Read(ref reader);
            CborValue value = Read(ref reader);
            context.obj.Add(key, value);
        }

        int ICborMapWriter<MapWriterContext>.GetMapSize(ref MapWriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.obj.Count;
        }

        bool ICborMapWriter<MapWriterContext>.WriteMapItem(ref CborWriter writer, ref MapWriterContext context)
        {
            if (context.enumerator.MoveNext())
            {
                KeyValuePair<CborValue, CborValue> pair = context.enumerator.Current;
                Write(ref writer, pair.Key);
                Write(ref writer, pair.Value);
                return true;
            }
            else
            {
                return false;
            }
        }

        void ICborArrayReader<ArrayReaderContext>.ReadBeginArray(int size, ref ArrayReaderContext context)
        {
            context.array = new CborArray();

            if (size != -1)
            {
                context.array.Capacity = size;
            }
        }

        void ICborArrayReader<ArrayReaderContext>.ReadArrayItem(ref CborReader reader, ref ArrayReaderContext context)
        {
            context.array.Add(Read(ref reader));
        }

        int ICborArrayWriter<ArrayWriterContext>.GetArraySize(ref ArrayWriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.array.Count;
        }

        bool ICborArrayWriter<ArrayWriterContext>.WriteArrayItem(ref CborWriter writer, ref ArrayWriterContext context)
        {
            if (context.array.Count > 0)
            {
                Write(ref writer, context.array[context.index++]);
                return context.index < context.array.Count;
            }

            return false;
        }

        CborObject? ICborConverter<CborObject?>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            MapReaderContext mapContext = new MapReaderContext();
            reader.ReadMap(this, ref mapContext);
            return mapContext.obj;
        }

        void ICborConverter<CborObject?>.Write(ref CborWriter writer, CborObject? value)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            Write(ref writer, value, LengthMode.Default);
        }

        void ICborConverter<CborObject?>.Write(ref CborWriter writer, CborObject? value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            MapWriterContext mapWriterContext = new MapWriterContext
            {
                obj = value,
                enumerator = _options.Deterministic
                    ? SortedEntries(value).GetEnumerator()
                    : value.GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.MapLengthMode
            };
            writer.WriteMap(this, ref mapWriterContext);
        }

        // Same shape as AbstractDictionaryConverter.SortedEntries: the key set is only known at write
        // time, so this materialises and sorts on every write. GetMapSize only reads context.obj.Count,
        // which sorting cannot change, so it needs no edit -- only which sequence the enumerator walks
        // differs.
        private static List<KeyValuePair<CborValue, CborValue>> SortedEntries(CborObject obj)
        {
            List<KeyValuePair<CborValue, CborValue>> entries = new List<KeyValuePair<CborValue, CborValue>>(obj);

            try
            {
                entries.Sort(CompareEntriesForDeterministicOrder);
            }
            catch (InvalidOperationException ex) when (ex.InnerException is CborException cborException)
            {
                // List<T>.Sort wraps any exception thrown by the comparison delegate in an
                // InvalidOperationException; unwrap so callers see the CborException itself, matching
                // AbstractDictionaryConverter.SortedEntries.
                throw cborException;
            }

            return entries;
        }

        private static int CompareEntriesForDeterministicOrder(
            KeyValuePair<CborValue, CborValue> x, KeyValuePair<CborValue, CborValue> y)
        {
            // CborValue exposes its underlying wire type via Type rather than via a distinct CLR type
            // per key kind the way Dictionary<TK, TV> did, so dispatch on that discriminator instead of
            // a CLR `is` pattern.
            CborValue keyX = x.Key;
            CborValue keyY = y.Key;

            if (keyX.Type == CborValueType.String && keyY.Type == CborValueType.String)
            {
                return CborKeyComparer.CompareTextKeys(
                    Encoding.UTF8.GetBytes(keyX.Value<string>()),
                    Encoding.UTF8.GetBytes(keyY.Value<string>()));
            }

            if (IsIntegerKey(keyX.Type) && IsIntegerKey(keyY.Type))
            {
                return CborKeyComparer.CompareIntKeys(keyX.Value<int>(), keyY.Value<int>());
            }

            throw new CborException(
                $"Deterministic encoding supports only string and int CborObject keys; found {keyX.Type} key.");
        }

        // CborPositive (major type 0) and CborNegative (major type 1) are both integer-keyed; CBOR
        // itself does not distinguish "positive int" and "negative int" as separate key kinds the way
        // CborValueType does.
        private static bool IsIntegerKey(CborValueType type)
        {
            return type == CborValueType.Positive || type == CborValueType.Negative;
        }

        CborArray? ICborConverter<CborArray?>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ArrayReaderContext arrayContext = new ArrayReaderContext();
            reader.ReadArray(this, ref arrayContext);
            return arrayContext.array;
        }

        void ICborConverter<CborArray?>.Write(ref CborWriter writer, CborArray? value)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            Write(ref writer, value, LengthMode.Default);
        }

        void ICborConverter<CborArray?>.Write(ref CborWriter writer, CborArray? value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            ArrayWriterContext arrayWriterContext = new ArrayWriterContext
            {
                array = value,
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.ArrayLengthMode
            };
            writer.WriteArray(this, ref arrayWriterContext);
        }
    }
}
