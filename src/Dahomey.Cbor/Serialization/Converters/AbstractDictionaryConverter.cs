using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public abstract class AbstractDictionaryConverter<TC, TK, TV> :
        CborConverterBase<TC>
        where TC : notnull, IDictionary<TK, TV>
        where TK : notnull
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<TK> _keyConverter;
        private readonly ICborConverter<TV> _valueConverter;

        protected abstract IDictionary<TK, TV> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(IDictionary<TK, TV> tempCollection);

        public AbstractDictionaryConverter(CborOptions options)
        {
            _options = options;
            _keyConverter = options.Registry.ConverterRegistry.Lookup<TK>();
            _valueConverter = options.Registry.ConverterRegistry.Lookup<TV>();
        }

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default!;
            }

            reader.ReadBeginMap();

            int remainingItemCount = reader.ReadSize();

            IDictionary<TK, TV> dict = InstantiateTempCollection();

            while (MoveNextMapItem(ref reader, ref remainingItemCount))
            {
                TK key = _keyConverter.Read(ref reader);
                TV value = _valueConverter.Read(ref reader);

                dict.Add(key, value);
            }

            reader.ReadEndMap();

            return InstantiateCollection(dict);
        }

        public override void Write(ref CborWriter writer, TC value, LengthMode lengthMode)
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

            foreach (KeyValuePair<TK, TV> pair in value)
            {
                _keyConverter.Write(ref writer, pair.Key);
                _valueConverter.Write(ref writer, pair.Value);
            }

            writer.WriteEndMap(size);
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
    }
}
