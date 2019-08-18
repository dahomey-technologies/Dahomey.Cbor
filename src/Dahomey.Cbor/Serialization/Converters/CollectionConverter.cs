using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CollectionConverter<TC, TI> :
        AbstractCollectionConverter<TC, TI>
        where TC : class, ICollection<TI>, new()
    {
        protected override TC InstantiateCollection(ICollection<TI> tempCollection)
        {
            return (TC)tempCollection;
        }

        protected override ICollection<TI> InstantiateTempCollection()
        {
            return new TC();
        }
    }
}
