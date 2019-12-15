using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0027
    {
        [CborDiscriminator("somediscriminator", Policy = CborDiscriminatorPolicy.Always)]
        public class Car
        {
            public string Description { get; set; }
        }

        [Fact]
        public void Test()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterConvention(new AttributeBasedDiscriminatorConvention<string>(options.Registry));

            var car = new Car()
            {
                Description = "n"
            };

            string hexBuffer = "A2625F7471736F6D656469736372696D696E61746F726B4465736372697074696F6E616E";
            Helper.TestWrite(car, hexBuffer, null, options);
        }
    }
}
