using Dahomey.Cbor.Util;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public class AnonymousObjectMappingConvention : IObjectMappingConvention
    {
        private static DefaultObjectMappingConvention _defaultObjectMappingConvention = new DefaultObjectMappingConvention();

        public void Apply<T>(SerializationRegistry registry, ObjectMapping<T> objectMapping)
        {
            Debug.Assert(typeof(T).IsAnonymous());

            // anonymous types have a single non default constructor
            ConstructorInfo constructorInfo = typeof(T).GetConstructors()[0];

            _defaultObjectMappingConvention.Apply(registry, objectMapping);
            objectMapping.MapCreator(constructorInfo);
        }
    }
}
