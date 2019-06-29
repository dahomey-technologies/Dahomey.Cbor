using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CollectionConverter<TC, TI> : ICborConverter<TC>
        where TC : class, ICollection<TI>, new()
    {
        private static readonly ICborConverter<TI> _itemConverter = CborConverter.Lookup<TI>();

        public TC Read(ref CborReader reader)
        {
            return reader.ReadArray<TC, TI>(_itemConverter);
        }

        public void Write(ref CborWriter writer, TC value)
        {
            writer.WriteArray(value, _itemConverter);
        }
    }
}
