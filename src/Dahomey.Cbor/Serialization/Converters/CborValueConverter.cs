using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CborValueConverter :
        CborConverterBase<CborValue>,
        ICborConverter<CborObject?>,
        ICborConverter<CborArray?>
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

        private void ReadBeginMap(int size, ref MapReaderContext context)
        {
            context.obj = new CborObject();
        }

        private void ReadMapItem(ref CborReader reader, ref MapReaderContext context)
        {
            CborValue key = Read(ref reader);
            CborValue value = Read(ref reader);
            context.obj.Add(key, value);
        }

        private int GetMapSize(ref MapWriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.obj.Count;
        }

        private static bool MoveNextMapItem(ref CborReader reader, ref int remainingItemCount)
        {
            if (remainingItemCount == 0 || remainingItemCount < 0 && reader.GetCurrentDataItemType() == CborDataItemType.Break)
            {
                return false;
            }

            remainingItemCount--;
            return true;
        }

        private bool WriteMapItem(ref CborWriter writer, ref MapWriterContext context)
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

        private void ReadBeginArray(int size, ref ArrayReaderContext context)
        {
            context.array = new CborArray();

            if (size != -1)
            {
                context.array.Capacity = size;
            }
        }

        private void ReadArrayItem(ref CborReader reader, ref ArrayReaderContext context)
        {
            context.array.Add(Read(ref reader));
        }

        private int GetArraySize(ref ArrayWriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.array.Count;
        }

        private bool WriteArrayItem(ref CborWriter writer, ref ArrayWriterContext context)
        {
            Write(ref writer, context.array[context.index++]);
            return context.index < context.array.Count;
        }

        CborObject? ICborConverter<CborObject?>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            MapReaderContext mapContext = new MapReaderContext();

            reader.ReadBeginMap();

            var remainingItemCount = reader.ReadSize();

            ReadBeginMap(remainingItemCount, ref mapContext);

            while (MoveNextMapItem(ref reader, ref remainingItemCount))
            {
                ReadMapItem(ref reader, ref mapContext);
            }

            reader.ReadEndMap();

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
                enumerator = value.GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.MapLengthMode
            };

            int size = GetMapSize(ref mapWriterContext);
            writer.WriteBeginMap(size);
            while (WriteMapItem(ref writer, ref mapWriterContext)) ;
            writer.WriteEndMap(size);
        }

        CborArray? ICborConverter<CborArray?>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ArrayReaderContext arrayContext = new ArrayReaderContext();

            reader.ReadBeginArray();

            int size = reader.ReadSize();

            ReadBeginArray(size, ref arrayContext);

            while (size > 0 || size < 0 && reader.GetCurrentDataItemType() != CborDataItemType.Break)
            {
                ReadArrayItem(ref reader, ref arrayContext);
                size--;
            }

            reader.ReadEndArray();

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

            int size = GetArraySize(ref arrayWriterContext);
            writer.WriteBeginArray(size);
            while (WriteArrayItem(ref writer, ref arrayWriterContext)) ;
            writer.WriteEndArray(size);
        }
    }
}
