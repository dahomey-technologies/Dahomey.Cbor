using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0051
    {
        public class ObjectMappingConventionProvider : IObjectMappingConventionProvider
        {
            private readonly IObjectMappingConvention _objectMappingConvention;

            public ObjectMappingConventionProvider()
            {
                _objectMappingConvention = new ObjectMappingConvention();
            }

            public IObjectMappingConvention GetConvention(Type type)
            {
                return _objectMappingConvention;
            }
        }

        public class ObjectMappingConvention : IObjectMappingConvention
        {
            private readonly DefaultObjectMappingConvention _defaultObjectMappingConvention = new DefaultObjectMappingConvention();

            public void Apply<T>(SerializationRegistry registry, ObjectMapping<T> objectMapping)
            {
                _defaultObjectMappingConvention.Apply<T>(registry, objectMapping);
                objectMapping.SetOrderBy(m => m.MemberName);
            }
        }

        public class MyObject
        {
            public int C { get; set; }
            public int A { get; set; }
            public int B { get; set; }
        }

        [Fact]
        public void TestWrite()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(new ObjectMappingConventionProvider());

            var obj = new MyObject
            {
                C = 1,
                A = 2,
                B = 3
            };

            // {"A": 2, "B": 3, "C": 1}
            const string hexBuffer = "A3614102614203614301";

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
