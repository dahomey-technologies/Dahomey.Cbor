using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedGuidHolder
    {
        public Guid Id { get; set; }
        public Guid? Optional { get; set; }
    }

    [CborSerializable(typeof(GeneratedGuidHolder))]
    public partial class GuidContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(GeneratedGuidHolder))]
    [CborSourceGenerationOptions(UntaggedGuidFormat = UntaggedGuidFormat.ClrByteOrder)]
    public partial class LegacyGuidContext : CborSerializerContext
    {
    }

    public class GuidTests
    {
        private static readonly Guid Sample = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        // D8 25 = tag 37, 50 = a byte string of 16.
        private const string SampleHex = "D82550" + "0123456789ABCDEF0123456789ABCDEF";

        /// <summary>
        /// The byte order is the whole point. RFC 9562 lays a UUID out big-endian, so the payload reads
        /// exactly as the canonical text does -- <c>01 23 45 67 89 ab ...</c>. The CLR's own
        /// <see cref="Guid.ToByteArray()"/> would put <c>67 45 23 01</c> there instead, and a document
        /// carrying that is not the UUID it claims to be to any other implementation of tag 37.
        /// </summary>
        [Fact]
        public void WriteIsBigEndianAsTheRfcRequires()
        {
            Helper.TestWrite(Sample, SampleHex);
        }

        [Fact]
        public void ReadIsBigEndianToo()
        {
            Assert.Equal(Sample, Helper.Read<Guid>(SampleHex));
        }

        /// <summary>
        /// A concrete guard against the swap being applied on one side only, which would round-trip
        /// perfectly while writing bytes no peer agrees with.
        /// </summary>
        [Fact]
        public void ThePayloadIsNotTheClrByteOrder()
        {
            string written = Helper.Write(Sample);

            Assert.Contains("0123456789ABCDEF", written, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("67452301", written, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
        [InlineData("01234567-89ab-cdef-0123-456789abcdef")]
        public void RoundTrips(string text)
        {
            Guid value = Guid.Parse(text);

            Assert.Equal(value, Helper.Read<Guid>(Helper.Write(value)));
        }

        private const string UntaggedClrHex = "50" + "67452301AB89EFCD0123456789ABCDEF";

        /// <summary>
        /// Sixteen untagged bytes cannot say which layout they are in, and the wrong guess is a valid,
        /// different Guid. So the default refuses, naming the cause and the remedy.
        /// </summary>
        [Fact]
        public void AnUntaggedByteStringIsRefusedByDefault()
        {
            CborException exception = Assert.Throws<CborException>(() => Helper.Read<Guid>(UntaggedClrHex));

            Assert.Contains("tag 37", exception.Message);
            Assert.Contains("UntaggedGuidFormat.ClrByteOrder", exception.Message);
        }

        /// <summary>
        /// The legacy bytes are built from <see cref="Guid.ToByteArray()"/> rather than typed out, and
        /// the same bytes read under the RFC's order are shown to give a different Guid -- which is the
        /// silent corruption the default exists to prevent.
        /// </summary>
        [Fact]
        public void TheOptInReadsLegacyClrOrderBytes()
        {
            string legacy = "50" + BitConverter.ToString(Sample.ToByteArray()).Replace("-", "");
            Assert.Equal(UntaggedClrHex, legacy);

            Guid wrongOrder = new Guid(SwapForRfc(Sample.ToByteArray()));
            Assert.NotEqual(Sample, wrongOrder);

            CborOptions options = new CborOptions { UntaggedGuidFormat = UntaggedGuidFormat.ClrByteOrder };

            Assert.Equal(Sample, Helper.Read<Guid>(legacy, options));
        }

        private static byte[] SwapForRfc(byte[] b)
        {
            (b[0], b[3]) = (b[3], b[0]);
            (b[1], b[2]) = (b[2], b[1]);
            (b[4], b[5]) = (b[5], b[4]);
            (b[6], b[7]) = (b[7], b[6]);
            return b;
        }

        /// <summary>The opt-in only concerns untagged strings: a tagged one is still big-endian.</summary>
        [Fact]
        public void TheOptInDoesNotChangeATaggedRead()
        {
            CborOptions options = new CborOptions { UntaggedGuidFormat = UntaggedGuidFormat.ClrByteOrder };

            Assert.Equal(Sample, Helper.Read<Guid>(SampleHex, options));
            Assert.Equal(SampleHex, Helper.Write(Sample, options));
        }

        [Fact]
        public void TheOptInReachesTheGeneratedContext()
        {
            LegacyGuidContext legacy = CborSerializerContext.Default<LegacyGuidContext>();
            GuidContext strict = CborSerializerContext.Default<GuidContext>();

            Assert.Equal(UntaggedGuidFormat.ClrByteOrder, legacy.Options.UntaggedGuidFormat);
            Assert.Equal(UntaggedGuidFormat.Refuse, strict.Options.UntaggedGuidFormat);

            string hex = "A2" + "624964" + UntaggedClrHex + "68" + "4F7074696F6E616C" + "F6";
            Assert.Equal(Sample, Helper.Read<GeneratedGuidHolder>(hex, legacy.Options).Id);
            Assert.Throws<CborException>(() => Helper.Read<GeneratedGuidHolder>(hex, strict.Options));
        }

        /// <summary>
        /// Not a form this writes, but the shape a peer emitting the textual rendering would send.
        /// </summary>
        [Fact]
        public void TheCanonicalTextFormIsRead()
        {
            Assert.Equal(
                Sample,
                Helper.Read<Guid>("7824" + BitConverter.ToString(
                    System.Text.Encoding.UTF8.GetBytes("01234567-89ab-cdef-0123-456789abcdef")).Replace("-", "")));
        }

        [Theory]
        [InlineData("4F" + "0123456789ABCDEF0123456789ABCD")]      // fifteen bytes
        [InlineData("5100" + "0123456789ABCDEF0123456789ABCDEF")]  // seventeen
        [InlineData("6C6E6F742D612D75756964")]                     // "not-a-uuid"
        [InlineData("01")]                                         // an integer
        [InlineData("D82A50" + "0123456789ABCDEF0123456789ABCDEF")] // tag 42, not 37
        [InlineData("D8254F" + "0123456789ABCDEF0123456789ABCD")]   // tag 37, fifteen bytes
        [InlineData("D82551" + "00" + "0123456789ABCDEF0123456789ABCDEF")] // tag 37, seventeen
        [InlineData("D825D82550" + "0123456789ABCDEF0123456789ABCDEF")] // two tags
        [InlineData("D8257824" + "30313233343536372D383961622D636465662D303132332D343536373839616263646566")] // tag 37 over text
        public void MalformedInputIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<Guid>(hexBuffer));
        }

        /// <summary>
        /// What this replaces. Without a converter the reflection path mapped the struct over its two
        /// public properties -- <c>Variant</c> and <c>Version</c> -- so the value itself never reached
        /// the document at all, and reading threw <see cref="ArgumentNullException"/>, which is not a
        /// <see cref="CborException"/> and so escaped any caller catching one.
        /// </summary>
        [Fact]
        public void TheValueSurvivesAPocoMember()
        {
            GeneratedGuidHolder value = new GeneratedGuidHolder { Id = Sample, Optional = Guid.Empty };

            GeneratedGuidHolder read = Helper.Read<GeneratedGuidHolder>(Helper.Write(value));

            Assert.Equal(Sample, read.Id);
            Assert.Equal(Guid.Empty, read.Optional);
        }

        /// <summary>
        /// The README documents Guid as the worked example of a converter for a type you do not own,
        /// registered through <c>[CborConverter]</c> or <c>SetConverter</c>. Both bypass the provider
        /// chain, so that example still governs and still produces its own bytes -- this addition makes
        /// it unnecessary for Guid, not broken.
        /// </summary>
        [Fact]
        public void AUserSuppliedConverterStillWins()
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverter(typeof(Guid), new GuidConverterOverride());

            Assert.Equal("6169", Helper.Write(Sample, options));
        }

        private class GuidConverterOverride : Dahomey.Cbor.Serialization.Converters.CborConverterBase<Guid>
        {
            public override Guid Read(ref CborReader reader) => Guid.Parse(reader.ReadString());

            public override void Write(ref CborWriter writer, Guid value) => writer.WriteString("i");
        }
    }
}
