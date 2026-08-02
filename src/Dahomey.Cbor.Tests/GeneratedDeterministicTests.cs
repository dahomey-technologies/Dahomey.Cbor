using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Members declared out of canonical order, so declaration order and RFC 8949 section 4.2.1 key
    /// order disagree and any path that writes declaration order is visible in the bytes.
    /// </summary>
    public class GeneratedOutOfOrder
    {
        public int Zebra { get; set; }
        public int Apple { get; set; }
        public int Mango { get; set; }
    }

    /// <summary>
    /// The int-keyed counterpart. A negative index is where plain ascending int order and canonical
    /// order part company: major type 1 (leading byte 0x20 and up) sorts after every major type 0 key,
    /// so -1 belongs last, not first.
    /// </summary>
    [CborObjectFormat(CborObjectFormat.IntKeyMap)]
    public class GeneratedIntKeyOutOfOrder
    {
        [CborProperty(-1)]
        public int Negative { get; set; }
        [CborProperty(0)]
        public int Zero { get; set; }
        [CborProperty(1)]
        public int One { get; set; }
    }

    /// <summary>
    /// A discriminated type: the discriminator entry is a member converter like any other, and has to
    /// take part in the same ordering.
    /// </summary>
    [CborDiscriminator("Disc", Policy = CborDiscriminatorPolicy.Always)]
    public class GeneratedDiscriminatedOutOfOrder
    {
        public int Zebra { get; set; }
        public int Apple { get; set; }
    }

    /// <summary>
    /// Positional format, where there are no keys to order and reordering would change what the
    /// document means rather than how it is spelled.
    /// </summary>
    [CborObjectFormat(CborObjectFormat.Array)]
    public class GeneratedArrayFormatOutOfOrder
    {
        [CborProperty(2)]
        public int Zebra { get; set; }
        [CborProperty(0)]
        public int Apple { get; set; }
        [CborProperty(1)]
        public int Mango { get; set; }
    }

    /// <summary>
    /// A context whose options carry <see cref="CborOptions.Deterministic"/>.
    /// </summary>
    [CborSerializable(typeof(GeneratedOutOfOrder))]
    [CborSerializable(typeof(GeneratedIntKeyOutOfOrder))]
    [CborSerializable(typeof(GeneratedDiscriminatedOutOfOrder))]
    [CborSerializable(typeof(GeneratedArrayFormatOutOfOrder))]
    public partial class DeterministicCborContext : CborSerializerContext
    {
        public DeterministicCborContext()
            : base(new CborOptions { Deterministic = true })
        {
        }
    }

    /// <summary>
    /// The same declarations registered into ordinary options, as the control: whatever these tests
    /// assert about the deterministic context has to be a consequence of the flag, not of the context.
    /// </summary>
    [CborSerializable(typeof(GeneratedOutOfOrder))]
    [CborSerializable(typeof(GeneratedIntKeyOutOfOrder))]
    [CborSerializable(typeof(GeneratedDiscriminatedOutOfOrder))]
    [CborSerializable(typeof(GeneratedArrayFormatOutOfOrder))]
    public partial class NonDeterministicCborContext : CborSerializerContext
    {
    }

    /// <summary>
    /// <see cref="CborOptions.Deterministic"/> has to mean the same thing on the generated path as on
    /// the reflection path. It does so without any generator involvement: the generated
    /// <c>Configure</c> registers ordinary <see cref="Serialization.Converters.ObjectConverter{T}"/>
    /// instances, and that converter picks its write order from the options each time a write starts
    /// rather than freezing it when it is built. Both paths therefore run the identical sort over
    /// identical member lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every assertion pairs a byte-identity check against the reflection path with a hex fixture.
    /// Byte-identity alone would be satisfied by both paths being wrong in the same way — which is
    /// exactly the outcome to expect here, since they share the sort — so the fixture is what pins the
    /// order to the one RFC 8949 section 4.2.1 defines.
    /// </para>
    /// <para>
    /// Sorting the member list in the generator instead, to spare the writer the runtime ordering,
    /// cannot replace this. The order is not a property of the type: <see cref="CborOptions.Deterministic"/>
    /// is settable at any point, including after a context has been constructed and cached, so an order
    /// baked in at generation time would be frozen at whatever the attribute said and silently wrong for
    /// every write that disagreed. It also could not be complete — the discriminator entry is not in the
    /// generator's member list at all (the emitted <c>SetDiscriminator</c> call inserts it at
    /// registration time, which is why
    /// <see cref="GeneratedContextSortsTheDiscriminatorWhenDeterministic"/> can order <c>"_t"</c> ahead of
    /// <c>"Apple"</c>), and <c>ObjectMapping.ValidateMemberNamesAndindexes</c> re-sorts IntKeyMap and
    /// Array members by ascending index at registration, discarding any generated order for those two
    /// formats outright.
    /// </para>
    /// </remarks>
    public class GeneratedDeterministicTests
    {
        private static readonly DeterministicCborContext Context =
            CborSerializerContext.Default<DeterministicCborContext>();

        private static readonly NonDeterministicCborContext PlainContext =
            CborSerializerContext.Default<NonDeterministicCborContext>();

        private static CborOptions ReflectionOptions() => new CborOptions { Deterministic = true };

        [Fact]
        public void GeneratedContextSortsStringKeyMembersWhenDeterministic()
        {
            GeneratedOutOfOrder value = new GeneratedOutOfOrder { Zebra = 1, Apple = 2, Mango = 3 };

            string reflection = Helper.Write(value, ReflectionOptions());
            string generated = Helper.Write(value, Context.Options);

            // A3 map(3)
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            //   655A65627261 "Zebra"  01
            Assert.Equal("A3654170706C6502654D616E676F03655A6562726101", generated, ignoreCase: true);
            Assert.Equal(reflection, generated);
        }

        /// <summary>
        /// The control: without the flag the generated path writes declaration order, so the fixture
        /// above is the flag's doing and not an accident of how the generator happens to emit members.
        /// </summary>
        [Fact]
        public void GeneratedContextKeepsDeclarationOrderWhenNotDeterministic()
        {
            GeneratedOutOfOrder value = new GeneratedOutOfOrder { Zebra = 1, Apple = 2, Mango = 3 };

            string reflection = Helper.Write(value);
            string generated = Helper.Write(value, PlainContext.Options);

            // A3 map(3)
            //   655A65627261 "Zebra"  01
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            Assert.Equal("A3655A6562726101654170706C6502654D616E676F03", generated, ignoreCase: true);
            Assert.Equal(reflection, generated);
        }

        [Fact]
        public void GeneratedContextSortsIntKeyMembersWhenDeterministic()
        {
            GeneratedIntKeyOutOfOrder value =
                new GeneratedIntKeyOutOfOrder { Negative = 7, Zero = 8, One = 9 };

            string reflection = Helper.Write(value, ReflectionOptions());
            string generated = Helper.Write(value, Context.Options);

            // A3 map(3)
            //   00  0   08
            //   01  1   09
            //   20 -1   07
            Assert.Equal("A3000801092007", generated, ignoreCase: true);
            Assert.Equal(reflection, generated);
        }

        [Fact]
        public void GeneratedContextSortsTheDiscriminatorWhenDeterministic()
        {
            CborOptions reflectionOptions = ReflectionOptions();
            reflectionOptions.Registry.DiscriminatorConventionRegistry
                .RegisterType(typeof(GeneratedDiscriminatedOutOfOrder));

            GeneratedDiscriminatedOutOfOrder value =
                new GeneratedDiscriminatedOutOfOrder { Zebra = 1, Apple = 2 };

            string reflection = Helper.Write(value, reflectionOptions);
            string generated = Helper.Write(value, Context.Options);

            // A3 map(3)
            //   625F74 "_t"          6444697363 "Disc"
            //   654170706C65 "Apple" 02
            //   655A65627261 "Zebra" 01
            Assert.Equal("A3625F746444697363654170706C6502655A6562726101", generated, ignoreCase: true);
            Assert.Equal(reflection, generated);
        }

        /// <summary>
        /// Array format writes members positionally, so declaration order is the meaning of the
        /// document and the flag must leave it alone on the generated path exactly as it does on the
        /// reflection path.
        /// </summary>
        [Fact]
        public void GeneratedContextLeavesArrayFormatInDeclarationOrderWhenDeterministic()
        {
            GeneratedArrayFormatOutOfOrder value =
                new GeneratedArrayFormatOutOfOrder { Zebra = 1, Apple = 2, Mango = 3 };

            string reflection = Helper.Write(value, ReflectionOptions());
            string generated = Helper.Write(value, Context.Options);

            // 83 array(3)
            //   02  Apple, index 0
            //   03  Mango, index 1
            //   01  Zebra, index 2
            Assert.Equal("83020301", generated, ignoreCase: true);
            Assert.Equal(reflection, generated);
        }

        [Fact]
        public void GeneratedContextRoundTripsWhenDeterministic()
        {
            GeneratedOutOfOrder value = new GeneratedOutOfOrder { Zebra = 1, Apple = 2, Mango = 3 };

            string hexBuffer = Helper.Write(value, Context.Options);
            GeneratedOutOfOrder actual = Cbor.Deserialize<GeneratedOutOfOrder>(
                hexBuffer.HexToBytes(), Context.Options);

            Assert.Equal(1, actual.Zebra);
            Assert.Equal(2, actual.Apple);
            Assert.Equal(3, actual.Mango);
        }
    }
}
