using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public abstract class AbstractCollectionConverter<TC, TI> :
        CborConverterBase<TC>,
        ICborArrayReader<AbstractCollectionConverter<TC, TI>.ReaderContext>,
        ICborArrayWriter<AbstractCollectionConverter<TC, TI>.WriterContext>
        where TC : ICollection<TI>
    {
        public struct ReaderContext
        {
            public ICollection<TI> collection;
        }

        public struct WriterContext
        {
            public int count;
            public IEnumerator<TI> enumerator;
        }

        private readonly ICborConverter<TI> _itemConverter;

        public AbstractCollectionConverter(SerializationRegistry registry)
        {
            _itemConverter = registry.ConverterRegistry.Lookup<TI>();
        }

        protected abstract ICollection<TI> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(ICollection<TI> tempCollection);

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadArray(this, ref context);

            return InstantiateCollection(context.collection);
        }

        public override void Write(ref CborWriter writer, TC value)
        {
            WriterContext context = new WriterContext
            {
                count = value.Count,
                enumerator = value.GetEnumerator()
            };
            writer.WriteArray(this, ref context);
        }

        public void ReadBeginArray(int size, ref ReaderContext context)
        {
            context.collection = InstantiateTempCollection();

            if (size != -1 && context.collection is List<TI> list)
            {
                list.Capacity = size;
            }
        }

        public void ReadArrayItem(ref CborReader reader, ref ReaderContext context)
        {
            TI item = _itemConverter.Read(ref reader);
            context.collection.Add(item);
        }

        public int GetArraySize(ref WriterContext context)
        {
            return context.count;
        }

        public void WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            if (context.enumerator.MoveNext())
            {
                _itemConverter.Write(ref writer, context.enumerator.Current);
            }
        }
    }
}
