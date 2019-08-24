using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class DictionaryConverter<TC, TK, TV> :
        AbstractDictionaryConverter<TC, TK, TV>
        where TC : class, IDictionary<TK, TV>, new()
    {
        public DictionaryConverter(SerializationRegistry registry)
            : base(registry)
        {
        }

        protected override TC InstantiateCollection(IDictionary<TK, TV> tempCollection)
        {
            return (TC)tempCollection;
        }

        protected override IDictionary<TK, TV> InstantiateTempCollection()
        {
            return new TC();
        }
    }
}
