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

        /// <summary>
        /// The tag is skipped like any other on the read path, so an untagged byte string of the right
        /// length is still a UUID -- which is what a peer that omits the tag sends.
        /// </summary>
        [Fact]
        public void AnUntaggedByteStringIsRead()
        {
            Assert.Equal(Sample, Helper.Read<Guid>("50" + "0123456789ABCDEF0123456789ABCDEF"));
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
