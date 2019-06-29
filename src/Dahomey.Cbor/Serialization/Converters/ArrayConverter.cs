using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ArrayConverter<TI> : ICborConverter<TI[]>
    {
        private static readonly ICborConverter<TI> _itemConverter = CborConverter.Lookup<TI>();

        public TI[] Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            reader.ReadBeginArray();

            int size = reader.ReadSize();
            TI[] array = new TI[size];

            for (int i = 0; i < size; i++)
            {
                TI item = _itemConverter.Read(ref reader);
                array[i] = item;
            }

            return array;
        }

        public void Write(ref CborWriter writer, TI[] value)
        {
            writer.WriteArray(value, _itemConverter);
        }
    }
}
