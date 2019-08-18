using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public abstract class AbstractDictionaryConverter<TC, TK, TV> :
        CborConverterBase<TC>, 
        ICborMapReader<AbstractDictionaryConverter<TC, TK, TV>.ReaderContext>,
        ICborMapWriter<AbstractDictionaryConverter<TC, TK, TV>.WriterContext>
        where TC : IDictionary<TK, TV>
    {
        public struct ReaderContext
        {
            public IDictionary<TK, TV> dict;
        }

        public struct WriterContext
        {
            public int count;
            public IEnumerator<KeyValuePair<TK, TV>> enumerator;
        }

        private static readonly ICborConverter<TK> _keyConverter = CborConverter.Lookup<TK>();
        private static readonly ICborConverter<TV> _valueConverter = CborConverter.Lookup<TV>();

        protected abstract IDictionary<TK, TV> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(IDictionary<TK, TV> tempCollection);

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadMap(this, ref context);
            return InstantiateCollection(context.dict);
        }

        public override void Write(ref CborWriter writer, TC value)
        {
            WriterContext context = new WriterContext
            {
                count = value.Count,
                enumerator = value.GetEnumerator()
            };
            writer.WriteMap(this, ref context);
        }

        public void ReadBeginMap(int size, ref ReaderContext context)
        {
            context.dict = InstantiateTempCollection();
        }

        public void ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            TK key = _keyConverter.Read(ref reader);
            TV value = _valueConverter.Read(ref reader);

            context.dict.Add(key, value);
        }

        public int GetMapSize(ref WriterContext context)
        {
            return context.count;
        }

        public void WriteMapItem(ref CborWriter writer, ref WriterContext context)
        {
            if (context.enumerator.MoveNext())
            {
                _keyConverter.Write(ref writer, context.enumerator.Current.Key);
                _valueConverter.Write(ref writer, context.enumerator.Current.Value);
            }
        }
    }
}
