using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CollectionConverter<TC, TI> : 
        ICborConverter<TC>,
        ICborArrayReader<CollectionConverter<TC, TI>.ReaderContext>,
        ICborArrayWriter<CollectionConverter<TC, TI>.WriterContext>
        where TC : class, ICollection<TI>, new()
    {
        public struct ReaderContext
        {
            public TC collection;
        }

        public struct WriterContext
        {
            public TC collection;
            public IEnumerator<TI> enumerator;
        }

        private static readonly ICborConverter<TI> _itemConverter = CborConverter.Lookup<TI>();

        public TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadArray(this, ref context);

            return context.collection;
        }

        public void Write(ref CborWriter writer, TC value)
        {
            WriterContext context = new WriterContext
            {
                collection = value,
                enumerator = value.GetEnumerator()
            };
            writer.WriteArray(this, ref context);
        }

        public void ReadBeginArray(int size, ref ReaderContext context)
        {
            context.collection = new TC();

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
            return context.collection.Count;
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
