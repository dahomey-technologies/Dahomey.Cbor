using Dahomey.Cbor.ObjectModel;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CborValueConverter : 
        ICborConverter<CborValue>,
        ICborMapReader<CborObject, object>,
        ICborMapWriter<CborValueConverter.MapWriterContext>
    {
        public struct MapWriterContext
        {
            public CborObject obj;
            public int pairIndex;
        }

        public CborValue Read(ref CborReader reader)
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
                    return reader.ReadArray<CborArray, CborValue>(this);

                case CborDataItemType.Map:
                    object context = null;
                    return reader.ReadMap(this, ref context);

                default:
                    throw reader.BuildException("Unexpected data item type");
            }
        }

        public void Write(ref CborWriter writer, CborValue value)
        {
            switch (value.Type)
            {
                case CborValueType.Object:
                    MapWriterContext context = new MapWriterContext
                    {
                        obj = (CborObject)value
                    };
                    writer.WriteMap(this, ref context);
                    break;

                case CborValueType.Array:
                    writer.WriteArray((CborArray)value, this);
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

        void ICborMapReader<CborObject, object>.ReadMapEntry(ref CborReader reader, ref CborObject obj, ref object context)
        {
            if (obj == null)
            {
                obj = new CborObject();
            }

            string key = reader.ReadString();
            CborValue value = Read(ref reader);
            obj.Pairs.Add(new CborPair(key, value));
        }

        int ICborMapWriter<MapWriterContext>.GetSize(ref MapWriterContext context)
        {
            return context.obj.Pairs.Count;
        }

        bool ICborMapWriter<MapWriterContext>.WriteMapEntry(ref CborWriter writer, ref MapWriterContext context)
        {
            CborPair pair = context.obj.Pairs[context.pairIndex++];
            writer.WriteString(pair.Name);
            Write(ref writer, pair.Value);

            return context.pairIndex < context.obj.Pairs.Count;
        }
    }
}
