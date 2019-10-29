using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Mappings;

namespace Dahomey.Cbor.Serialization
{
    public class SerializationRegistry
    {
        public CborConverterRegistry ConverterRegistry { get; }
        public ObjectMappingRegistry ObjectMappingRegistry { get; }
        public ObjectMappingConventionRegistry ObjectMappingConventionRegistry { get; }
        public DiscriminatorConventionRegistry DiscriminatorConventionRegistry { get; }

        public SerializationRegistry()
        {
            ConverterRegistry = new CborConverterRegistry(this);
            ObjectMappingRegistry = new ObjectMappingRegistry(this);
            ObjectMappingConventionRegistry = new ObjectMappingConventionRegistry();
            DiscriminatorConventionRegistry = new DiscriminatorConventionRegistry(this);
        }
    }
}
