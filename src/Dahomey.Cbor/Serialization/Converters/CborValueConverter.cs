using Dahomey.Cbor.ObjectModel;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CborValueConverter :
        CborConverterBase<CborValue>,
        ICborConverter<CborObject>,
        ICborConverter<CborArray>,
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
            public IEnumerator<KeyValuePair<string, CborValue>> enumerator;
        }

        public struct ArrayReaderContext
        {
            public CborArray array;
        }

        public struct ArrayWriterContext
        {
            public CborArray array;
            public int index;
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

                case CborDataItemType.String:
                    return reader.ReadString();

                case CborDataItemType.Array:
                    return ((ICborConverter<CborArray>)this).Read(ref reader);

                case CborDataItemType.Map:
                    return ((ICborConverter<CborObject>)this).Read(ref reader);

                default:
                    throw reader.BuildException("Unexpected data item type");
            }
        }

        public override void Write(ref CborWriter writer, CborValue value)
        {
            switch (value.Type)
            {
                case CborValueType.Object:
                    ((ICborConverter<CborObject>)this).Write(ref writer, (CborObject)value);
                    break;

                case CborValueType.Array:
                    ((ICborConverter<CborArray>)this).Write(ref writer, (CborArray)value);
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

                case CborValueType.String:
                    writer.WriteString(value.Value<string>());
                    break;

                case CborValueType.Boolean:
                    writer.WriteBoolean(value.Value<bool>());
                    break;

                case CborValueType.Null:
                    writer.WriteNull();
                    break;
            }
        }

        void ICborMapReader<MapReaderContext>.ReadBeginMap(int size, ref MapReaderContext context)
        {
            context.obj = new CborObject();
        }

        void ICborMapReader<MapReaderContext>.ReadMapItem(ref CborReader reader, ref MapReaderContext context)
        {
            string key = reader.ReadString();
            CborValue value = Read(ref reader);
            context.obj.Add(key, value);
        }

        int ICborMapWriter<MapWriterContext>.GetMapSize(ref MapWriterContext context)
        {
            return context.obj.Count;
        }

        void ICborMapWriter<MapWriterContext>.WriteMapItem(ref CborWriter writer, ref MapWriterContext context)
        {
            if (context.enumerator.MoveNext())
            {
                KeyValuePair<string, CborValue> pair = context.enumerator.Current;
                writer.WriteString(pair.Key);
                Write(ref writer, pair.Value);
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
            return context.array.Count;
        }

        void ICborArrayWriter<ArrayWriterContext>.WriteArrayItem(ref CborWriter writer, ref ArrayWriterContext context)
        {
            Write(ref writer, context.array[context.index++]);
        }

        CborObject ICborConverter<CborObject>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            MapReaderContext mapContext = new MapReaderContext();
            reader.ReadMap(this, ref mapContext);
            return mapContext.obj;
        }

        public void Write(ref CborWriter writer, CborObject value)
        {
            MapWriterContext mapWriterContext = new MapWriterContext
            {
                obj = value,
                enumerator = value.GetEnumerator()
            };
            writer.WriteMap(this, ref mapWriterContext);
        }

        CborArray ICborConverter<CborArray>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ArrayReaderContext arrayContext = new ArrayReaderContext();
            reader.ReadArray(this, ref arrayContext);
            return arrayContext.array;
        }

        public void Write(ref CborWriter writer, CborArray value)
        {
            ArrayWriterContext arrayWriterContext = new ArrayWriterContext
            {
                array = value
            };
            writer.WriteArray(this, ref arrayWriterContext);
        }
    }
}
