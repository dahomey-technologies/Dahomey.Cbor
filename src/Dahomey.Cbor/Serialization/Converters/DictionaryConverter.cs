using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class DictionaryConverter<TC, TK, TV> :
        CborConverterBase<TC>, 
        ICborMapReader<DictionaryConverter<TC, TK, TV>.ReaderContext>,
        ICborMapWriter<DictionaryConverter<TC, TK, TV>.WriterContext>
        where TC : class, IDictionary<TK, TV>, new()
    {
        public struct ReaderContext
        {
            public TC dict;
        }

        public struct WriterContext
        {
            public TC dict;
            public IEnumerator<KeyValuePair<TK, TV>> enumerator;
        }

        private static readonly ICborConverter<TK> _keyConverter = CborConverter.Lookup<TK>();
        private static readonly ICborConverter<TV> _valueConverter = CborConverter.Lookup<TV>();

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadMap(this, ref context);
            return context.dict;
        }

        public override void Write(ref CborWriter writer, TC value)
        {
            WriterContext context = new WriterContext
            {
                dict = value,
                enumerator = value.GetEnumerator()
            };
            writer.WriteMap(this, ref context);
        }

        public void ReadBeginMap(int size, ref ReaderContext context)
        {
            context.dict = new TC();
        }

        public void ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            TK key = _keyConverter.Read(ref reader);
            TV value = _valueConverter.Read(ref reader);

            context.dict.Add(key, value);
        }

        public int GetMapSize(ref WriterContext context)
        {
            return context.dict.Count;
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
