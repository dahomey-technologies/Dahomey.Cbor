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
            _objectMappings.AddOrUpdate(objectMapping.ObjectType, objectMapping,
                (type, existingObjectMapping) => objectMapping);

            IMappingInitialization mappingInitialization = objectMapping as IMappingInitialization;
            if (mappingInitialization != null)
            {
                mappingInitialization.Initialize();
            }

            if (objectMapping.Discriminator != null)
            {
                _registry.DiscriminatorConventionRegistry.RegisterType(objectMapping.ObjectType);
            }
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
            return Lookup(typeof(T));
        }

        public IObjectMapping Lookup(Type type)
        {
            IObjectMapping objectMapping = _objectMappings.GetOrAdd(type, t => CreateDefaultObjectMapping(type));

            if (objectMapping is IMappingInitialization mappingInitialization)
            {
                mappingInitialization.Initialize();
            }

            return objectMapping;
        }

        private IObjectMapping CreateDefaultObjectMapping(Type type)
        {
            IObjectMapping objectMapping =
                (IObjectMapping)Activator.CreateInstance(typeof(ObjectMapping<>).MakeGenericType(type), _registry);

            objectMapping.AutoMap();

            return objectMapping;
        }
    }
}
