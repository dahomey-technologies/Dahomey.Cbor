using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class DiscriminatorTests
    {
        [Fact]
        public void ReadPolymorphicObject()
        {
            const string hexBuffer = "A16A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F62496401";
            BaseObjectHolder obj = Helper.Read<BaseObjectHolder>(hexBuffer);
        }

        [Theory]
        [InlineData(CborDiscriminatorPolicy.Default, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Auto, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Never, "A26A426173654F626A656374A2644E616D6563666F6F624964016A4E616D654F626A656374A2644E616D656362617262496402")]
        [InlineData(CborDiscriminatorPolicy.Always, "A26A426173654F626A656374A3625F746A4E616D654F626A656374644E616D6563666F6F624964016A4E616D654F626A656374A3625F746A4E616D654F626A656374644E616D656362617262496402")]
        public void WritePolymorphicObject(CborDiscriminatorPolicy discriminatorPolicy, string hexBuffer)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<NameObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(discriminatorPolicy);
            });
            options.Registry.ObjectMappingRegistry.Register<BaseObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(discriminatorPolicy);
            });

            BaseObjectHolder obj = new BaseObjectHolder
            {
                BaseObject = new NameObject
                {
                    Id = 1,
                    Name = "foo"
                },
                NameObject = new NameObject
                {
                    Id = 2,
                    Name = "bar"
                }
            };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
