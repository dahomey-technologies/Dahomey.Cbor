using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0029
    {
        public abstract class DummyParticle
        {

        }

        [CborDiscriminator("radix.particles.message", Policy = CborDiscriminatorPolicy.Always)]
        public class DummyMessageParticle : DummyParticle
        {
            public long nonce { get; protected set; }
        }

        [Fact]
        public void Test()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(DummyMessageParticle));

            // {"nonce": 2181035975144481159, "_t": "radix.particles.message"}
            const string hexBuffer = "A2656E6F6E63651B1E4498A9ECD5A187625F747772616469782E7061727469636C65732E6D657373616765";
            DummyParticle obj = Helper.Read<DummyParticle>(hexBuffer, options);
        }
    }
}
