using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class DictionaryConverter<TK, TV> : 
        ICborConverter<Dictionary<TK, TV>>, 
        ICborMapReader<Dictionary<TK, TV>, object>,
        ICborMapWriter<DictionaryConverter<TK, TV>.MapWriterContext>
    {
        public struct MapWriterContext
        {
            public IDictionary<TK, TV> dict;
            public IEnumerator<KeyValuePair<TK, TV>> enumerator;
        }

        private static readonly ICborConverter<TK> _keyConverter = CborConverter.Lookup<TK>();
        private static readonly ICborConverter<TV> _valueConverter = CborConverter.Lookup<TV>();

        public Dictionary<TK, TV> Read(ref CborReader reader)
        {
            object context = null;
            return reader.ReadMap(this, ref context);
        }

        public void Write(ref CborWriter writer, Dictionary<TK, TV> value)
        {
            MapWriterContext context = new MapWriterContext
            {
                dict = value,
                enumerator = value.GetEnumerator()
            };
            writer.WriteMap(this, ref context);
        }

        void ICborMapReader<Dictionary<TK, TV>, object>.ReadMapEntry(ref CborReader reader, ref Dictionary<TK, TV> obj, ref object context)
        {
            if (obj == null)
            {
                obj = new Dictionary<TK, TV>();
            }

            TK key = _keyConverter.Read(ref reader);
            TV value = _valueConverter.Read(ref reader);

            obj.Add(key, value);
        }

        public int GetSize(ref MapWriterContext context)
        {
            return context.dict.Count;
        }

        public bool WriteMapEntry(ref CborWriter writer, ref MapWriterContext context)
        {
            if (!context.enumerator.MoveNext())
            {
                return false;
            }

            _keyConverter.Write(ref writer, context.enumerator.Current.Key);
            _valueConverter.Write(ref writer, context.enumerator.Current.Value);
            return true;
        }
    }
}
