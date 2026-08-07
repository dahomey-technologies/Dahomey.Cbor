using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class MutualA
    {
        public int Id { get; set; }
        public MutualB Peer { get; set; }
    }

    public class MutualB
    {
        public int Id { get; set; }
        public MutualA Peer { get; set; }
    }

    [CborSerializable(typeof(MutualA))]
    [CborSerializable(typeof(MutualB))]
    [CborSourceGenerationOptions(NamingConvention = typeof(CamelCaseNamingConvention))]
    public partial class MutualRecursionContext : CborSerializerContext
    {
    }

    /// <summary>
    /// A cycle between two declared types must still end up bound to the generated converters.
    /// </summary>
    /// <remarks>
    /// The registration order the generator emits breaks a cycle by emitting one participant first.
    /// Constructing that participant's <c>ObjectConverter</c> resolves its members eagerly, so the
    /// other participant is looked up before its own registration line has run, and the provider
    /// chain builds a reflection converter for it. Whether the generated registration then replaces
    /// that converter is what these tests pin: on CoreCLR a reflection converter still produces
    /// output, so nothing else in the suite notices, while under Native AOT the same lookup is the
    /// <c>MakeGenericType</c> path the context exists to avoid.
    /// <para>
    /// The naming convention is the observable: it is baked into the generated member mappings, and
    /// a converter built by the reflection path instead uses the declared names.
    /// </para>
    /// </remarks>
    public class GeneratedMutualRecursionTests
    {
        private static readonly MutualRecursionContext Context =
            CborSerializerContext.Default<MutualRecursionContext>();

        private static MutualA SampleGraph()
        {
            MutualA a = new MutualA { Id = 1 };
            a.Peer = new MutualB { Id = 2 };
            return a;
        }

        /// <summary>
        /// Both participants must honour the context's naming convention. A cycle participant bound
        /// to a reflection converter writes its members under their declared names, so a single
        /// document comes out with two naming conventions in it.
        /// </summary>
        [Fact]
        public void BothCycleParticipantsUseTheContextNamingConvention()
        {
            string hexBuffer = Helper.Write(SampleGraph(), Context.Options);

            // a2                     map(2)
            //    62 6964             "id"
            //    01                  1
            //    64 70656572         "peer"
            //    a2                  map(2)
            //       62 6964          "id"
            //       02               2
            //       64 70656572      "peer"
            //       f6               null
            Assert.Equal("A2626964016470656572A2626964026470656572F6", hexBuffer);
        }

        /// <summary>
        /// The typed accessor must hand back the generated converter, not one the provider chain
        /// built while breaking the cycle.
        /// </summary>
        [Fact]
        public void CycleParticipantsRoundTripThroughTheGeneratedContext()
        {
            string hexBuffer = Helper.Write(SampleGraph(), Context.Options);
            MutualA rehydrated = Cbor.Deserialize<MutualA>(
                Extensions.StringExtensions.HexToBytes(hexBuffer), Context.Options);

            Assert.Equal(1, rehydrated.Id);
            Assert.Equal(2, rehydrated.Peer.Id);
            Assert.Null(rehydrated.Peer.Peer);
        }
    }
}
