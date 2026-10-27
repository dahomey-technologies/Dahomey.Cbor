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

        /// <summary>{"_t": 1, "Id": 7} -- KnownChannel.</summary>
        private const string KnownSubtype = "A2625F740162496407";

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

        /// <summary>
        /// The case the registry-level tests cannot see: a converter that already read a document holds
        /// the convention it resolved, and clearing the registry's cache does not reach it.
        /// </summary>
        [Fact]
        public void AConventionRegisteredAfterAReadGovernsTheNextRead()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();

            // {"_t": 1, "Id": 7}
            Assert.IsType<KnownChannel>(Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options));

            registry.ClearConventions();
            registry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(FallbackChannel)));
            registry.RegisterType<KnownChannel>();

            Assert.IsType<FallbackChannel>(Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// Clearing and registering nothing after a read leaves the hierarchy undiscriminated for the
        /// converters that already exist, not governed by the convention they resolved earlier.
        /// </summary>
        [Fact]
        public void ClearingAfterAReadStopsTheConverterUsingTheClearedConvention()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();
            Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options);

            registry.ClearConventions();

            // The discriminator is now just an unknown member of an abstract type.
            Assert.ThrowsAny<System.Exception>(
                () => Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options));

            // And a convention registered later governs it again.
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry));
            registry.RegisterType<KnownChannel>();
            Assert.IsType<KnownChannel>(Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// A type that resolved to no convention at all, read after a clear that leaves it that way,
        /// and then a discriminated read once a convention is registered.
        /// </summary>
        [Fact]
        public void ClearingBeforeAnyConventionIsResolvedThenReadingADiscriminatedDocument()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;

            registry.ClearConventions();
            Assert.Null(registry.GetConvention(typeof(PlainType)));
            Assert.Equal(3, Cbor.Deserialize<PlainType>("A162496403".HexToBytes(), options).Id);

            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry));
            registry.RegisterType<KnownChannel>();

            Assert.IsType<KnownChannel>(Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// Readers racing a clear-and-reregister settle on the registry's final answer, and none of them
        /// throws anything but the refusal an unregistered discriminator is entitled to.
        /// </summary>
        [Fact]
        public void ReadsRacingAClearSettleOnTheFinalConvention()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();
            Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options);

            using var stop = new System.Threading.CancellationTokenSource();
            var readers = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
            for (int i = 0; i < 4; i++)
            {
                readers.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        try { Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options); }
                        catch (CborException) { }
                    }
                }));
            }

            for (int i = 0; i < 200; i++)
            {
                registry.ClearConventions();
                registry.RegisterConvention(
                    new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(FallbackChannel)));
                registry.RegisterType<KnownChannel>();
            }

            stop.Cancel();
            System.Threading.Tasks.Task.WaitAll(readers.ToArray());

            Assert.IsType<FallbackChannel>(Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// The write side holds a convention too, in the discriminator's member converter.
        /// </summary>
        [Fact]
        public void AConventionRegisteredAfterAWriteGovernsTheNextWrite()
        {
            CborOptions options = new CborOptions { DiscriminatorPolicy = CborDiscriminatorPolicy.Always };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();

            byte[] before = Write(new KnownChannel { Id = 7 }, options);
            Assert.Equal(KnownSubtype.HexToBytes(), before);

            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "kind"));
            registry.RegisterType<KnownChannel>();

            // {"kind": 1, "Id": 7}
            byte[] after = Write(new KnownChannel { Id = 7 }, options);
            Assert.Equal("A264" + "6B696E64" + "01" + "62" + "4964" + "07", System.BitConverter.ToString(after).Replace("-", ""));
        }

        private static byte[] Write(BaseChannel value, CborOptions options)
        {
            using var stream = new System.IO.MemoryStream();
            Cbor.SerializeAsync<BaseChannel>(value, stream, options).GetAwaiter().GetResult();
            return stream.ToArray();
        }

        public class PlainType
        {
            public int Id { get; set; }
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
