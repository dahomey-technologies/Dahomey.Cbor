using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ArrayConverter<TI> :
        CborConverterBase<TI[]?>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<TI> _itemConverter;

        public ArrayConverter(CborOptions options)
        {
            _options = options;
            _itemConverter = options.Registry.ConverterRegistry.Lookup<TI>();
        }

        public override TI[]? Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            reader.ReadBeginArray();

            int size = reader.ReadSize();

            TI[]? array = null;
            List<TI>? list = null;
            int index = 0;

            if (size != -1)
            {
                array = new TI[size];
            }
            else
            {
                list = new List<TI>();
            }

            while (size > 0 || size < 0 && reader.GetCurrentDataItemType() != CborDataItemType.Break)
            {
                TI item = _itemConverter.Read(ref reader);

                if (array != null)
                {
                    array[index++] = item;
                }
                else
                {
                    list.Add(item);
                }

                size--;
            }

            reader.ReadEndArray();

            if (array != null)
            {
                return array;
            }
            else
            {
                return list.ToArray();
            }
        }

        public override void Write(ref CborWriter writer, TI[]? value, LengthMode lengthMode)
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

            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : value.Length;
            writer.WriteBeginArray(size);

            for (int index = 0; index < value.Length;)
            {
                _itemConverter.Write(ref writer, value[index++]);
            }

            writer.WriteEndArray(size);
        }
    }
}
