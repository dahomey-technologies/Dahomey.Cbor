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

        private static bool MoveNextMapItem(ref CborReader reader, ref int remainingItemCount)
        {
            if (remainingItemCount == 0 || remainingItemCount < 0 && reader.GetCurrentDataItemType() == CborDataItemType.Break)
            {
                return false;
            }

            remainingItemCount--;
            return true;
        }

        CborObject? ICborConverter<CborObject?>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            reader.ReadBeginMap();

            var remainingItemCount = reader.ReadSize();

            CborObject obj = new CborObject();

            while (MoveNextMapItem(ref reader, ref remainingItemCount))
            {
                CborValue key = Read(ref reader);
                CborValue value = Read(ref reader);
                obj.Add(key, value);
            }

            reader.ReadEndMap();

            return obj;
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

            if (lengthMode == LengthMode.Default)
            {
                lengthMode = _options.MapLengthMode;
            }

            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : value.Count;
            writer.WriteBeginMap(size);

            foreach (KeyValuePair<CborValue, CborValue> pair in value)
            {
                Write(ref writer, pair.Key);
                Write(ref writer, pair.Value);
            }

            writer.WriteEndMap(size);
        }

        CborArray? ICborConverter<CborArray?>.Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            reader.ReadBeginArray();

            int size = reader.ReadSize();

            CborArray array = new CborArray();

            if (size != -1)
            {
                array.Capacity = size;
            }

            while (size > 0 || size < 0 && reader.GetCurrentDataItemType() != CborDataItemType.Break)
            {
                array.Add(Read(ref reader));
                size--;
            }

            reader.ReadEndArray();

            return array;
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

            if (lengthMode == LengthMode.Default)
            {
                lengthMode = _options.ArrayLengthMode;
            }

            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : value.Count;
            writer.WriteBeginArray(size);

            for (int index = 0; index < value.Count;)
            {
                Write(ref writer, value[index++]);
            }

            writer.WriteEndArray(size);
        }
    }
}
