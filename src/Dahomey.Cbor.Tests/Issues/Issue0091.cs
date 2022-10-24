#if NET6_0_OR_GREATER

using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0091
    {
        public abstract record class Animal(DateTime Timestamp)
        {
        }

        [CborDiscriminator("dog")]
        public record class Dog(string Color, DateTime Timestamp) : Animal(Timestamp)
        {
        }

        [Fact]
        public void TestReadWrite()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new AttributeBasedDiscriminatorConvention<string>(options.Registry));
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(Dog));

            Dog dog = new("black", DateTime.Parse("2022-10-24T14:05:08Z"));
            var cbor = Helper.Write<Animal>(dog, options);

            Animal deserialized = Helper.Read<Animal>(cbor, options);

            Assert.NotNull(deserialized);
            Dog dog2 = Assert.IsType<Dog>(deserialized);
            Assert.Equal(dog.Color, dog2.Color);
            Assert.Equal(dog.Timestamp, dog2.Timestamp);
        }
    }
}

#endif