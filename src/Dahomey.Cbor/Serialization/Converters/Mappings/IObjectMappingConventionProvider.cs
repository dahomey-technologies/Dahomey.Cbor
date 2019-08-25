using System;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IObjectMappingConventionProvider
    {
        IObjectMappingConvention GetConvention(Type type);
    }
}
