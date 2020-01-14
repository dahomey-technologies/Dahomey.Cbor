using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class InterfaceDictionaryConverter<TK, TV> :
        AbstractDictionaryConverter<IDictionary<TK, TV>, TK, TV>
        where TK : notnull
    {
        public InterfaceDictionaryConverter(SerializationRegistry registry)
            : base(registry)
        {
        }

        protected override IDictionary<TK, TV> InstantiateCollection(IDictionary<TK, TV> tempCollection)
        {
            return tempCollection;
        }

        protected override IDictionary<TK, TV> InstantiateTempCollection()
        {
            return new Dictionary<TK, TV>();
        }
    }
}
