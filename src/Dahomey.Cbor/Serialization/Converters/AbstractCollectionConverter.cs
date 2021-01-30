using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Serialization.Converters
{
    public abstract class AbstractCollectionConverter<TC, TI> :
        CborConverterBase<TC>
        where TC : IEnumerable<TI>
    {
        private readonly CborOptions _options;
        private readonly ICborConverter<TI> _itemConverter;

        public AbstractCollectionConverter(CborOptions options)
        {
            _options = options;
            _itemConverter = options.Registry.ConverterRegistry.Lookup<TI>();
        }

        protected abstract ICollection<TI> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(ICollection<TI> tempCollection);

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default!;
            }

            reader.ReadBeginArray();

            int size = reader.ReadSize();

            ICollection<TI> collection = InstantiateTempCollection();

            if (size != -1 && collection is List<TI> list)
            {
                list.Capacity = size;
            }

            while (size > 0 || size < 0 && reader.GetCurrentDataItemType() != CborDataItemType.Break)
            {
                TI item = _itemConverter.Read(ref reader);
                collection.Add(item);
                size--;
            }

            reader.ReadEndArray();

            return InstantiateCollection(collection);
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
                lengthMode = _options.ArrayLengthMode;
            }

            int size = lengthMode == LengthMode.IndefiniteLength ? -1 : value.Count();
            writer.WriteBeginArray(size);

            foreach (TI item in value)
            {
                _itemConverter.Write(ref writer, item);
            }

            writer.WriteEndArray(size);
        }
    }
}
