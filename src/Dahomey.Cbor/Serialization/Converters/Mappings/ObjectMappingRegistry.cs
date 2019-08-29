using System;
using System.Collections.Concurrent;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class ObjectMappingRegistry
    {
        private readonly ConcurrentDictionary<Type, IObjectMapping> _objectMappings 
            = new ConcurrentDictionary<Type, IObjectMapping>();
        private readonly SerializationRegistry _registry;

        public ObjectMappingRegistry(SerializationRegistry registry)
        {
            _registry = registry;
        }
        
        public void Register(IObjectMapping objectMapping)
        {
            if (objectMapping is IMappingInitialization mappingInitialization)
            {
                mappingInitialization.Initialize();
            }

            _objectMappings.AddOrUpdate(objectMapping.ObjectType, objectMapping,
                (type, existingObjectMapping) => objectMapping);
        }

        public void Register<T>() where T : class
        {
            ObjectMapping<T> objectMapping = new ObjectMapping<T>(_registry);
            Register(objectMapping);
        }

        public void Register<T>(Action<ObjectMapping<T>> initializer) where T : class
        {
            ObjectMapping<T> objectMapping = new ObjectMapping<T>(_registry);
            initializer(objectMapping);
            Register(objectMapping);
        }

        public IObjectMapping Lookup<T>() where T : class
        {
            return _objectMappings.GetOrAdd(typeof(T), t => CreateDefaultObjectMapping<T>());
        }

        private IObjectMapping CreateDefaultObjectMapping<T>() where T : class
        {
            ObjectMapping<T> objectMapping = new ObjectMapping<T>(_registry);
            objectMapping.AutoMap();

            if (objectMapping is IMappingInitialization mappingInitialization)
            {
                mappingInitialization.Initialize();
            }

            return objectMapping;
        }
    }
}
