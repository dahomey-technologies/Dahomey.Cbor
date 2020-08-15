using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class InterfaceCollectionConverter<TCollection, TInterface, TItem> :
        AbstractCollectionConverter<TInterface, TItem>
        where TInterface : IEnumerable<TItem>
        where TCollection : class, ICollection<TItem>, new()
    {
        public InterfaceCollectionConverter(CborOptions options)
            : base(options)
        {
        }

        protected override TInterface InstantiateCollection(ICollection<TItem> tempCollection)
        {
            return (TInterface)tempCollection;
        }

        protected override ICollection<TItem> InstantiateTempCollection()
        {
            return new TCollection();
        }
    }
}
