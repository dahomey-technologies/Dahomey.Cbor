using System;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// https://github.com/dahomey-technologies/Dahomey.Cbor/issues/21
    /// </summary>
    public class Issue0021
    {
        public class LowerCaseNamingConvention : INamingConvention
        {
            public string GetPropertyName(string name)
            {
                return name.ToLowerInvariant();
            }
        }

        public class LowerCaseObjectMappingConvention : IObjectMappingConvention
        {
            private readonly IObjectMappingConvention defaultObjectMappingConvention = new DefaultObjectMappingConvention();
            private readonly INamingConvention lowerCaseNamingConvention = new LowerCaseNamingConvention();

            public void Apply<T>(SerializationRegistry registry, ObjectMapping<T> objectMapping) where T : class
            {
                defaultObjectMappingConvention.Apply<T>(registry, objectMapping);
                objectMapping.SetNamingConvention(lowerCaseNamingConvention);
            }
        }

        public class LowerCaseObjectMappingConventionProvider : IObjectMappingConventionProvider
        {
            private readonly IObjectMappingConvention _objectMappingConvention = new LowerCaseObjectMappingConvention();

            public IObjectMappingConvention GetConvention(Type type)
            {
                return _objectMappingConvention;
            }
        }

        [Fact]
        public void TestWrite()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(new LowerCaseObjectMappingConventionProvider());

            const string hexBuffer = "A168696E7476616C75650C"; // {"intvalue": 12}
            Helper.TestWrite(new IntObject { IntValue = 12 }, hexBuffer, null, options);
        }

        [Fact]
        public void TestRead()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingConventionRegistry.RegisterProvider(new LowerCaseObjectMappingConventionProvider());

            const string hexBuffer = "A168696E7476616C75650C"; // {"intvalue": 12}
            IntObject intObject = Helper.Read<IntObject>(hexBuffer, options);

            Assert.NotNull(intObject);
            Assert.Equal(12, intObject.IntValue);
        }
    }
}
