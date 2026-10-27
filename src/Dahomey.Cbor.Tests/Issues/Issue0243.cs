using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            Assert.IsType<KnownChannel>(Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options));

            registry.ClearConventions();
            registry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(options.Registry, "_t", typeof(FallbackChannel)));
            registry.RegisterType<KnownChannel>();

            Assert.IsType<FallbackChannel>(Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// Clearing and registering nothing after a read leaves the hierarchy undiscriminated for the
        /// converters that already exist, rather than governed by the convention they resolved earlier.
        /// </summary>
        [Fact]
        public void ClearingAfterAReadStopsTheConverterUsingTheClearedConvention()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();
            Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options);

            registry.ClearConventions();

            // The discriminator is now an unknown member of an abstract type, and nothing says how to
            // build one. The refusal is the library's own, not an accident of a stale convention.
            Assert.Throws<CborException>(
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
        /// The write side holds a convention too, in the discriminator's member converter.
        /// </summary>
        [Fact]
        public void AConventionRegisteredAfterAWriteGovernsTheNextWrite()
        {
            CborOptions options = new CborOptions { DiscriminatorPolicy = CborDiscriminatorPolicy.Always };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();

            Assert.Equal(KnownSubtype.HexToBytes(), Write(new KnownChannel { Id = 7 }, options));

            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "kind"));
            registry.RegisterType<KnownChannel>();

            // {"kind": 1, "Id": 7}
            Assert.Equal(
                "A2646B696E6401624964" + "07",
                Convert.ToHexString(Write(new KnownChannel { Id = 7 }, options)));
        }

        /// <summary>
        /// The discriminator mapping reports the member name of the convention now in force. That is
        /// what the mapping's other consumers read, so it is pinned directly.
        /// </summary>
        [Fact]
        public void TheDiscriminatorMappingFollowsTheConventionInForce()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();

            IDiscriminatorMapping mapping = options.Registry.ObjectMappingRegistry.Lookup<KnownChannel>()
                .MemberMappings.OfType<IDiscriminatorMapping>().Single();
            Assert.Equal("_t", mapping.MemberName);

            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "kind"));
            registry.RegisterType<KnownChannel>();

            Assert.Equal("kind", mapping.MemberName);
        }

        /// <summary>
        /// A discriminator name that a fresh <c>CborOptions</c> refuses when the mapping is built must
        /// not become writable because the convention arrived later.
        /// </summary>
        [Fact]
        public void ADiscriminatorNameCollidingWithAMemberIsRefusedAfterTheFact()
        {
            const string Expected = "maps several fields/properties to the member name 'Id'";

            // What fresh options say.
            CborOptions fresh = new CborOptions { DiscriminatorPolicy = CborDiscriminatorPolicy.Always };
            fresh.Registry.DiscriminatorConventionRegistry.ClearConventions();
            fresh.Registry.DiscriminatorConventionRegistry.RegisterConvention(
                new DefaultDiscriminatorConvention<int>(fresh.Registry, "Id"));
            fresh.Registry.DiscriminatorConventionRegistry.RegisterType<KnownChannel>();
            CborException freshException = Assert.Throws<CborException>(() => Write(new KnownChannel { Id = 7 }, fresh));
            Assert.Contains(Expected, freshException.Message);

            // The same registrations arriving after the type was mapped and written once.
            CborOptions options = new CborOptions { DiscriminatorPolicy = CborDiscriminatorPolicy.Always };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();
            Write(new KnownChannel { Id = 7 }, options);

            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "Id"));
            registry.RegisterType<KnownChannel>();

            CborException written = Assert.Throws<CborException>(() => Write(new KnownChannel { Id = 7 }, options));
            Assert.Contains(Expected, written.Message);

            CborException read = Assert.Throws<CborException>(
                () => Cbor.Deserialize<KnownChannel>(KnownSubtype.HexToBytes(), options));
            Assert.Contains(Expected, read.Message);
        }

        /// <summary>
        /// A member converter handed a convention that the registry has since replaced does not trust
        /// it: the caller resolved it before the constructor ran, so the version it is stamped with has
        /// to be one the registry has not reached.
        /// </summary>
        [Fact]
        public void AMemberConverterBuiltFromAnOutdatedConventionAsksAgain()
        {
            CborOptions options = new CborOptions();
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            DefaultDiscriminatorConvention<int> outdated = new DefaultDiscriminatorConvention<int>(options.Registry, "old");

            registry.ClearConventions();
            registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(options.Registry, "kind"));
            registry.RegisterType<KnownChannel>();

            DiscriminatorMemberConverter<KnownChannel> converter = new DiscriminatorMemberConverter<KnownChannel>(
                options, outdated, CborDiscriminatorPolicy.Always, CborObjectFormat.StringKeyMap);

            Assert.Equal("kind", System.Text.Encoding.UTF8.GetString(converter.MemberName));
        }

        /// <summary>
        /// The version a resolution is stamped with is the one taken before the lookup. A convention
        /// that changes the registry from inside the lookup - here by swapping the conventions and
        /// registering a subtype - makes the answer it returns outdated on arrival, and stamping it
        /// with the version read afterwards would call it current for good.
        /// </summary>
        [Fact]
        public void ARegistrationDuringAResolutionIsNotStampedAsAccountedFor()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterConvention(new SwappingConvention(options));

            // The converter for BaseChannel is built, and resolves, inside this call. The resolution
            // finds nothing and swaps the conventions on its way, and the read has to notice.
            Assert.IsType<FallbackChannel>(Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options));
        }

        /// <summary>
        /// Readers racing a series of clears and re-registrations, each installing a different fallback,
        /// refuse only with a <see cref="CborException"/> and settle on the last one installed.
        /// </summary>
        [Fact]
        public async Task ReadsRacingAClearSettleOnTheFinalConventionAsync()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.Silent };
            DiscriminatorConventionRegistry registry = options.Registry.DiscriminatorConventionRegistry;
            registry.RegisterType<KnownChannel>();
            Cbor.Deserialize<BaseChannel>(KnownSubtype.HexToBytes(), options);

            using CancellationTokenSource stop = new CancellationTokenSource();
            ConcurrentBag<Exception> unexpected = new ConcurrentBag<Exception>();
            int reads = 0;

            List<Task> readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options);
                    }
                    catch (CborException)
                    {
                        // Between a clear and the registration that follows, nothing resolves.
                    }
                    catch (Exception exception)
                    {
                        unexpected.Add(exception);
                    }

                    Interlocked.Increment(ref reads);
                }
            })).ToList();

            // Nothing is changed until every reader has been through the loop at least once.
            while (Volatile.Read(ref reads) < 16)
            {
                await Task.Yield();
            }

            for (int i = 0; i < 200; i++)
            {
                registry.ClearConventions();
                registry.RegisterConvention(new DefaultDiscriminatorConvention<int>(
                    options.Registry, "_t", i % 2 == 0 ? typeof(FallbackChannel) : typeof(OtherFallbackChannel)));
                registry.RegisterType<KnownChannel>();
            }

            await stop.CancelAsync();
            await Task.WhenAll(readers);

            Assert.Empty(unexpected);
            Assert.IsType<OtherFallbackChannel>(Cbor.Deserialize<BaseChannel>(UnknownSubtype.HexToBytes(), options));
        }

        private static byte[] Write(BaseChannel value, CborOptions options)
        {
            ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();
            Cbor.Serialize<BaseChannel>(value, buffer, options);
            return buffer.WrittenSpan.ToArray();
        }

        /// <summary>
        /// Resolves nothing, but the first time it is asked about the base channel it replaces the
        /// conventions and registers a subtype under the new one, which is what makes the base resolve
        /// from then on.
        /// </summary>
        private sealed class SwappingConvention : IDiscriminatorConvention
        {
            private static readonly byte[] Name = { (byte)'_', (byte)'t' };
            private readonly CborOptions _options;
            private bool _swapped;

            public SwappingConvention(CborOptions options) => _options = options;

            public ReadOnlySpan<byte> MemberName => Name;

            public Type ReadDiscriminator(ref CborReader reader) => throw new NotSupportedException();

            public void WriteDiscriminator(ref CborWriter writer, Type actualType) => throw new NotSupportedException();

            public bool TryRegisterType(Type type)
            {
                if (type == typeof(BaseChannel) && !_swapped)
                {
                    _swapped = true;

                    DiscriminatorConventionRegistry registry = _options.Registry.DiscriminatorConventionRegistry;
                    registry.ClearConventions();
                    registry.RegisterConvention(
                        new DefaultDiscriminatorConvention<int>(_options.Registry, "_t", typeof(FallbackChannel)));
                    registry.RegisterType<KnownChannel>();
                }

                return false;
            }
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

        public class OtherFallbackChannel : BaseChannel
        {
        }
    }
}
