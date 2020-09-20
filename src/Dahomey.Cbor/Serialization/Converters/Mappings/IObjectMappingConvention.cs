using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IObjectMappingConvention
    {
        void Apply<T>(SerializationRegistry registry, ObjectMapping<T> objectMapping);
    }
}
