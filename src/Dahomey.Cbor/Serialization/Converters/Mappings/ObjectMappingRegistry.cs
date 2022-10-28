using System;
using System.Collections.Concurrent;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class ObjectMappingRegistry
    {
        private readonly ConcurrentDictionary<Type, IObjectMapping> _objectMappings
            = new ConcurrentDictionary<Type, IObjectMapping>();
        private readonly SerializationRegistry _registry;
        private readonly CborOptions _options;

        public ObjectMappingRegistry(SerializationRegistry registry, CborOptions options)
        {
            _registry = registry;
            _options = options;
        }

        public void Register(IObjectMapping objectMapping)
        {
            _objectMappings.AddOrUpdate(objectMapping.ObjectType, objectMapping,
                (type, existingObjectMapping) => objectMapping);

            if (objectMapping.Discriminator != null)
            {
                _registry.DiscriminatorConventionRegistry.RegisterType(objectMapping.ObjectType);
            }
        }

        public void Register<T>()
        {
            ObjectMapping<T> objectMapping = new ObjectMapping<T>(_registry, _options);
            Register(objectMapping);
        }

        public void Register<T>(Action<ObjectMapping<T>> initializer)
        {
            ObjectMapping<T> objectMapping = new ObjectMapping<T>(_registry, _options);
            initializer(objectMapping);
            Register(objectMapping);
        }

        public IObjectMapping Lookup<T>()
        {
            return Lookup(typeof(T));
        }

        public IObjectMapping Lookup(Type type)
        {
            return _objectMappings.GetOrAdd(type, t => CreateDefaultObjectMapping(type));
        }

        private IObjectMapping CreateDefaultObjectMapping(Type type)
        {
            Type objectMappingType = typeof(ObjectMapping<>).MakeGenericType(type);

            IObjectMapping? objectMapping =
                (IObjectMapping?)Activator.CreateInstance(objectMappingType, _registry, _options);

            if (objectMapping == null)
            {
                throw new CborException($"Cannot instantiate {objectMappingType}");
            }

            objectMapping.AutoMap();

            return objectMapping;
        }
    }
}
