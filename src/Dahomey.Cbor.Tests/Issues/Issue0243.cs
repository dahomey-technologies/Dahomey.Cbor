using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// <c>ClearConventions</c> emptied the convention stack but left the per-type cache populated, so a
    /// type whose convention had already been resolved kept the cleared one — and a convention
    /// registered afterwards never governed it.
    /// </summary>
    /// <remarks>
    /// The window this closed was narrower than it looked. Anything that resolves the hierarchy first
    /// closes it: a previous <c>RegisterType</c>, an earlier read, or constructing a
    /// <c>CborSerializerContext</c>, whose generated <c>Configure</c> builds every declared converter
    /// before the caller can reach its <c>Options</c>.
    /// </remarks>
    public class Issue0243
    {
        /// <summary>{"_t": 99, "Id": 7} — a discriminator no type here registers.</summary>
        private const string UnknownSubtype = "A2625F74186362496407";

        [Fact]
        public void AConventionRegisteredAfterAResolutionGoverns()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            // Resolve the hierarchy first, which is what caches a convention for the base type.
            registry.RegisterType<KnownChannel>();

            registry.ClearConventions();
            registry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(FallbackChannel)));
            registry.RegisterType<KnownChannel>();

            BaseChannel channel = Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options);

            Assert.IsType<FallbackChannel>(channel);
            Assert.Equal(7, channel.Id);
        }

        /// <summary>
        /// The same on options nothing has touched, which worked before and has to keep working — the
        /// fix must not make the first registration depend on a prior resolution either.
        /// </summary>
        [Fact]
        public void AConventionRegisteredBeforeAnyResolutionStillGoverns()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            registry.ClearConventions();
            registry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(FallbackChannel)));
            registry.RegisterType<KnownChannel>();

            Assert.IsType<FallbackChannel>(
                Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// Clearing and registering nothing leaves a hierarchy undiscriminated, rather than leaving the
        /// old conventions quietly in force. This is the half a cache that outlives the clear hides.
        /// </summary>
        [Fact]
        public void ClearingWithoutRegisteringLeavesNothingResolving()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            registry.RegisterType<KnownChannel>();
            registry.ClearConventions();

            Assert.Null(registry.GetConvention(typeof(BaseChannel)));
        }

        public abstract class BaseChannel
        {
            public int Id { get; set; }
        }

        [CborIntDiscriminator(1)]
        public class KnownChannel : BaseChannel
        {
        }

        public class FallbackChannel : BaseChannel
        {
        }
    }
}
