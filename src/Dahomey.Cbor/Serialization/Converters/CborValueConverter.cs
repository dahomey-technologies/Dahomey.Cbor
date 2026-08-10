using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;

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

            /// <summary>
            /// <see cref="CborOptions.DuplicateKeyMode"/> as it stood when this map started, so that
            /// one map is read under one policy.
            /// </summary>
            /// <remarks>
            /// The option is settable at any time, including on the process-wide
            /// <see cref="CborOptions.Default"/>, so reading it per entry would let a change made
            /// while a document is being read - by a custom converter, or by another thread - refuse
            /// one repeated key in a map and overwrite on the next.
            /// </remarks>
            public bool lastWins;
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

            CborValue cborValue = ReadCborValue(ref reader);

            // WithSemanticTag rather than assigning SemanticTag: small integers, common floats, both
            // booleans, the empty string and null are shared instances, and stamping a tag on one of
            // those would tag every occurrence of that value in the process.
            return hasSemanticTag ? cborValue.WithSemanticTag(semanticTag) : cborValue;
        }

        private CborValue ReadCborValue(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Boolean:
                    return reader.ReadBoolean();

                case CborDataItemType.Null:
                    // Null covers `undefined` as well as `null`, but ReadNull accepts only the latter:
                    // for F7 it returns false having consumed nothing, and the item still has to be
                    // taken or the next read is handed the same header back.
                    if (!reader.ReadNull())
                    {
                        reader.SkipDataItem();
                    }

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
            // Containers emit their own tag: the map and array writers are reachable without passing
            // through here at all - a member declared CborObject resolves straight to them - so
            // emitting it for them here as well would double the tag on that route.
            if (value.Type != CborValueType.Object && value.Type != CborValueType.Array)
            {
                WriteSemanticTag(ref writer, value);
            }

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

        /// <summary>
        /// Re-emits the semantic tag <c>Read</c> captured, so a document read into the object model and
        /// written back says what it said. The tag is often the meaning rather than an annotation - tag
        /// 1 makes an integer a datetime, tag 39 carries a discriminator - so losing it changed the
        /// document silently.
        /// </summary>
        /// <remarks>
        /// Unconditional, which is what makes the round trip carry the tag rather than lose it. One tag,
        /// not a chain: <see cref="CborValue.SemanticTag"/> is a single <c>ulong?</c>, so a nested
        /// <c>C1 C2 01</c> keeps the outer tag and drops the inner -- a limit of the model rather than
        /// of this method. A caller that reads a tag-1 value
        /// and replaces it with a string does get a tag that no longer matches its content, but that is
        /// the caller describing their own value: there is no way to tell an edited value from an
        /// untouched one, so dropping the tag on suspicion would break the round trip this exists for.
        /// </remarks>
        private static void WriteSemanticTag(ref CborWriter writer, CborValue value)
        {
            if (value.SemanticTag.HasValue)
            {
                writer.WriteSemanticTag(value.SemanticTag.Value);
            }
        }

        void ICborMapReader<MapReaderContext>.ReadBeginMap(int size, ref MapReaderContext context)
        {
            context.obj = new CborObject();
            context.lastWins = _options.DuplicateKeyMode == DuplicateKeyMode.LastWins;
        }

        void ICborMapReader<MapReaderContext>.ReadMapItem(ref CborReader reader, ref MapReaderContext context)
        {
            CborValue key = Read(ref reader);
            CborValue value = Read(ref reader);

            if (context.lastWins)
            {
                // An indexer assignment rather than Add: under LastWins a repeated key overwrites
                // instead of throwing, and Add has no overwriting form.
                context.obj[key] = value;
                return;
            }

            // Caught rather than pre-checked with ContainsKey: a duplicate is malformed input, so the
            // cost belongs on that path and not on the lookup every well-formed pair would pay. The
            // document was already refused here; without this it is refused as an ArgumentException,
            // which is outside the CborException a caller wraps deserialization in.
            try
            {
                context.obj.Add(key, value);
            }
            catch (ArgumentException exception)
            {
                // A CborValue key is never null -- a CBOR null decodes to CborValue.Null, an instance --
                // so the remaining cases are a duplicate or the dictionary refusing for another reason.
                throw reader.BuildException(context.obj.ContainsKey(key)
                    ? MapKeyErrors.Duplicate(key)
                    : MapKeyErrors.Rejected(key, exception.Message));
            }
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
            // Before ReadNull, which skips a semantic tag as every read entry point does. Reaching the
            // container converter directly - Cbor.Deserialize<CborObject> - bypasses Read(CborValue),
            // so without this the tag is consumed and lost on the way in and there is nothing left for
            // the write path to re-emit.
            bool hasSemanticTag = reader.TryReadSemanticTag(out ulong semanticTag);

            if (reader.ReadNull())
            {
                return null;
            }

            MapReaderContext mapContext = new MapReaderContext();
            reader.ReadMap(this, ref mapContext);

            if (hasSemanticTag)
            {
                mapContext.obj.SemanticTag = semanticTag;
            }

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

            WriteSemanticTag(ref writer, value);

            MapWriterContext mapWriterContext = new MapWriterContext
            {
                obj = value,
                enumerator = _options.Deterministic
                    ? SortedEntries(value)
                    : value.GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.MapLengthMode
            };
            writer.WriteMap(this, ref mapWriterContext);
        }

        // Same shape as AbstractDictionaryConverter.SortedEntries: the key set is only known at write
        // time, so this materialises and sorts on every write. GetMapSize only reads context.obj.Count,
        // which sorting cannot change, so it needs no edit -- only which sequence the enumerator walks
        // differs. A CborObject may mix key kinds, which is why the order comes from
        // DeterministicKeyOrder (major type first) rather than from a same-kind-only comparison.
        private IEnumerator<KeyValuePair<CborValue, CborValue>> SortedEntries(CborObject obj)
        {
            KeyValuePair<CborValue, CborValue>[] sorted =
                DeterministicKeyOrder.Sort(obj, this, _options.MaxDepth);

            return ((IEnumerable<KeyValuePair<CborValue, CborValue>>)sorted).GetEnumerator();
        }

        CborArray? ICborConverter<CborArray?>.Read(ref CborReader reader)
        {
            bool hasSemanticTag = reader.TryReadSemanticTag(out ulong semanticTag);

            if (reader.ReadNull())
            {
                return null;
            }

            ArrayReaderContext arrayContext = new ArrayReaderContext();
            reader.ReadArray(this, ref arrayContext);

            if (hasSemanticTag)
            {
                arrayContext.array.SemanticTag = semanticTag;
            }

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

            WriteSemanticTag(ref writer, value);

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
