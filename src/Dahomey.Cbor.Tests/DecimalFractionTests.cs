using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedDecimalHolder
    {
        public decimal Value { get; set; }
        public decimal? Optional { get; set; }
        public Dictionary<decimal, string> Keyed { get; set; }
    }

    /// <summary>
    /// A generated context declaring the setting, which is the half a run-time test cannot reach: the
    /// generated registrations resolve <c>decimal</c> through the same converter provider, so the
    /// option has to survive the attribute-to-emitted-code round trip to have any effect there.
    /// </summary>
    [CborSerializable(typeof(GeneratedDecimalHolder))]
    [CborSourceGenerationOptions(DecimalFormat = DecimalFormat.DecimalFraction)]
    public partial class GeneratedDecimalFractionContext : CborSerializerContext
    {
    }

    /// <summary>
    /// RFC 8949 §3.4.4 decimal fractions: tag 4 over <c>[exponent, mantissa]</c>, which is what a
    /// <see cref="decimal"/> looks like to every implementation but this one.
    /// </summary>
    /// <remarks>
    /// The two halves are deliberately asymmetric, and these tests pin that. Writing tag 4 is opt-in
    /// through <see cref="CborOptions.DecimalFormat"/>, because it moves the bytes of every document
    /// with a decimal in it; reading it is unconditional, because it was a <see cref="CborException"/>
    /// before and accepting it takes nothing away. So every read case here runs on default options.
    /// <para>
    /// The hex is the interoperable form rather than this library's own: <c>C48221196AB3</c> is what
    /// <c>System.Formats.Cbor</c> writes for <c>273.15</c> and reads back as it, which is the whole
    /// point of the exercise.
    /// </para>
    /// </remarks>
    public class DecimalFractionTests
    {
        private static CborOptions InteroperableOptions()
        {
            return new CborOptions { DecimalFormat = DecimalFormat.DecimalFraction };
        }

        private static decimal Parse(string value)
        {
            return decimal.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        public class ObjectWithDecimal
        {
            [CborProperty("v")]
            public decimal Value { get; set; }
        }

        public class ObjectWithNullableDecimal
        {
            [CborProperty("v")]
            public decimal? Value { get; set; }
        }

        [Theory]
        // C4 tag(4) 82 array(2) <exponent> <mantissa>
        [InlineData("C48221196AB3", "273.15")]
        [InlineData("C48221396AB2", "-273.15")]
        [InlineData("C4820001", "1")]
        [InlineData("C482221903E8", "1.000")]
        [InlineData("C482271B00013D2B9C2D125A", "3487324.89798234")]
        [InlineData("C4822F1B0DED017037E8D195", "100.3459873459458453")]
        [InlineData("C482381B01", "0.0000000000000000000000000001")]
        // A 96-bit mantissa does not fit an integer header, so it goes out under the bignum tags -
        // C2 4C for the positive extreme and C3 4C for the negative one.
        [InlineData("C48200C24CFFFFFFFFFFFFFFFFFFFFFFFF", "79228162514264337593543950335")]
        [InlineData("C48200C34CFFFFFFFFFFFFFFFFFFFFFFFE", "-79228162514264337593543950335")]
        public void WritesTheDecimalFraction(string hexBuffer, string value)
        {
            Helper.TestWrite(Parse(value), hexBuffer, null, InteroperableOptions());
        }

        /// <summary>
        /// The scale is part of what tag 4 carries, so the distinction the DecimalFloat form keeps
        /// between <c>0m</c> and <c>0.00m</c> survives the change of encoding.
        /// </summary>
        [Theory]
        [InlineData("C4820000", "0")]
        [InlineData("C4822100", "0.00")]
        public void WritesTheScaleOfZero(string hexBuffer, string value)
        {
            Helper.TestWrite(Parse(value), hexBuffer, null, InteroperableOptions());
        }

        /// <summary>
        /// Default options write what they have always written: 0xFC plus sixteen raw bytes. This is
        /// the same vector as <c>CborWriterTests.WriteDecimal</c>, here to say out loud that adding
        /// the setting moved no existing document.
        /// </summary>
        [Fact]
        public void TheDefaultFormatIsUnchanged()
        {
            Helper.TestWrite(3487324.89798234m, "FC00013D2B9C2D125A0000000000080000");
        }

        [Fact]
        public void WriteDecimalOnTheWriterIsUnchanged()
        {
            // The single-argument overload is not options-driven, so a caller writing straight to a
            // CborWriter keeps the bytes it had.
            Helper.TestWrite(nameof(Serialization.CborWriter.WriteDecimal), 3487324.89798234m,
                "FC00013D2B9C2D125A0000000000080000", null);
        }

        [Theory]
        [InlineData("C48221196AB3", "273.15")]
        [InlineData("C48221396AB2", "-273.15")]
        [InlineData("C4820000", "0")]
        [InlineData("C4822100", "0.00")]
        [InlineData("C4820001", "1")]
        [InlineData("C482221903E8", "1.000")]
        [InlineData("C482271B00013D2B9C2D125A", "3487324.89798234")]
        [InlineData("C482381B01", "0.0000000000000000000000000001")]
        [InlineData("C48200C24CFFFFFFFFFFFFFFFFFFFFFFFF", "79228162514264337593543950335")]
        [InlineData("C48200C34CFFFFFFFFFFFFFFFFFFFFFFFE", "-79228162514264337593543950335")]
        [InlineData("C482381BC24CFFFFFFFFFFFFFFFFFFFFFFFF", "7.9228162514264337593543950335")]
        // Not the preferred encoding of the mantissa - 27315 fits an integer header - but a decoder
        // has no business refusing it. C2 42 6AB3 is tag 2 over a two-byte magnitude.
        [InlineData("C48221C2426AB3", "273.15")]
        public void ReadsTheDecimalFraction(string hexBuffer, string expectedValue)
        {
            Helper.TestRead(hexBuffer, Parse(expectedValue));
        }

        /// <summary>
        /// A mantissa with trailing zeros is in range even when its exponent is not: the factors of
        /// ten are divided out, so a producer that does not normalize is still readable.
        /// </summary>
        /// <remarks>
        /// Out of range on either end of the same operation, which is why the last three rows are here.
        /// A scale past 28 has to give factors of ten back; so does a mantissa too wide for 96 bits,
        /// and <c>[-1, 10^29]</c> is inside the scale limit while being too wide - it is the 1E+28 a
        /// <c>decimal</c> holds at a scale of 0. Reducing only for the scale rejected it.
        /// </remarks>
        [Theory]
        // C482 381D 1864 -> [-30, 100], which is 1E-28 and fits.
        [InlineData("C482381D1864", "0.0000000000000000000000000001")]
        // C482 381C 0A -> [-29, 10], the same value one factor of ten out.
        [InlineData("C482381C0A", "0.0000000000000000000000000001")]
        [InlineData("C48220C24D01431E0FAE6D7217CAA0000000", "10000000000000000000000000000")]  // [-1, 10^29]
        [InlineData("C48221C24D0C9F2C9CD04674EDEA40000000", "10000000000000000000000000000")]  // [-2, 10^30]
        [InlineData("C4823827C2520125DFA371A19E6F7CB54395CA0000000000", "10")]                // [-40, 10^41]
        public void ReadsAnUnnormalizedMantissa(string hexBuffer, string expectedValue)
        {
            Helper.TestRead(hexBuffer, Parse(expectedValue));
        }

        /// <summary>
        /// Zero is zero at every exponent, so no exponent puts it out of range. The scale is kept
        /// where the type has room for it and dropped where it does not, which costs nothing: the
        /// value is the same either way.
        /// </summary>
        [Theory]
        [InlineData("C482186400", "0")]      // [100, 0]
        [InlineData("C4823A000186A000", "0")] // [-100001, 0]
        public void ReadsZeroAtAnyExponent(string hexBuffer, string expectedValue)
        {
            Helper.TestRead(hexBuffer, Parse(expectedValue));
        }

        /// <summary>
        /// §3.4.4 says the content is an array of two items; it does not say the length has to be
        /// definite, and a streamed pair denotes the same value.
        /// </summary>
        [Fact]
        public void ReadsAnIndefiniteLengthDecimalFraction()
        {
            // C4 tag(4) 9F array(*) 21 -2 196AB3 27315 FF break
            Helper.TestRead("C49F21196AB3FF", 273.15m);
        }

        /// <summary>
        /// A foreign tag around the decimal fraction is skipped as one is anywhere else, and a tag 4
        /// anywhere in the stack still says decimal fraction - the same rule
        /// <c>ReadBigInteger</c> applies to tags 2 and 3.
        /// </summary>
        [Theory]
        [InlineData("C1C48221196AB3")] // tag 1 outside tag 4
        [InlineData("C4C18221196AB3")] // tag 1 inside tag 4
        public void ReadsThroughATagStack(string hexBuffer)
        {
            Helper.TestRead(hexBuffer, 273.15m);
        }

        [Theory]
        // Out of range: decimal holds a 96-bit mantissa at a scale of 0 to 28.
        [InlineData("C482381D1865")                        ] // [-30, 101] - no trailing zero to give
        [InlineData("C482181D01")                          ] // [29, 1] - 1E29 is past decimal.MaxValue
        [InlineData("C48200C24D01000000000000000000000000")] // [0, 2^96] - one bit too wide
        // Still too wide once every factor of ten the scale can give back has been given back: 1E+29
        // is past decimal.MaxValue, and 2^96 is past the mantissa by one.
        [InlineData("C48220C24D0C9F2C9CD04674EDEA40000000")] // [-1, 10^30]
        [InlineData("C48220C24D0A000000000000000000000000")] // [-1, 2^96 * 10]
        [InlineData("C4821B00000002540BE40001")             ] // [10000000000, 1] - exponent past int
        // Malformed: the content of tag 4 is a two-element array of integers.
        [InlineData("C48321196AB300")]   // three items
        [InlineData("C49F21196AB300FF")] // three items, indefinite length
        [InlineData("C4196AB3")]         // not an array at all
        [InlineData("C482216548656C6C6F")] // mantissa is a text string
        public void RejectsWhatIsNotAReadableDecimal(string hexBuffer)
        {
            Helper.TestRead<decimal>(hexBuffer, typeof(CborException));
        }

        /// <summary>
        /// The rejection message names an oversized mantissa by its digit count rather than by its
        /// digits. Rendering a <see cref="System.Numerics.BigInteger"/> is a base conversion, quadratic
        /// in the digit count, so a message spelling one out costs far more than the decode it reports
        /// on - about two seconds for a 33 KB document that is otherwise rejected in a millisecond.
        /// </summary>
        [Fact]
        public void AnOversizedMantissaIsNotSpelledOutInTheMessage()
        {
            using ByteBufferWriter buffer = new ByteBufferWriter();
            CborWriter writer = new CborWriter(buffer);

            writer.WriteSemanticTag(4);
            writer.WriteBeginArray(2);
            writer.WriteInt32(-1);
            writer.WriteBigInteger(BigInteger.Pow(10, 400) * 7);

            byte[] document = buffer.WrittenSpan.ToArray();
            string message = null;

            try
            {
                CborReader reader = new CborReader(document);
                reader.ReadDecimal();
            }
            catch (CborException exception)
            {
                message = exception.Message;
            }

            Assert.NotNull(message);
            Assert.Contains("401-digit mantissa", message);
            Assert.True(message.Length < 200, message);
        }

        [Fact]
        public void RoundTripsThroughAMember()
        {
            // A1 map(1) 6176 "v" C4 82 21 196AB3
            const string hexBuffer = "A16176C48221196AB3";

            Helper.TestWrite(new ObjectWithDecimal { Value = 273.15m }, hexBuffer, null, InteroperableOptions());
            Assert.Equal(273.15m, Helper.Read<ObjectWithDecimal>(hexBuffer).Value);
        }

        [Fact]
        public void RoundTripsThroughANullableMember()
        {
            const string hexBuffer = "A16176C48221196AB3";

            Helper.TestWrite(new ObjectWithNullableDecimal { Value = 273.15m }, hexBuffer, null, InteroperableOptions());
            Assert.Equal(273.15m, Helper.Read<ObjectWithNullableDecimal>(hexBuffer).Value);

            Helper.TestWrite(new ObjectWithNullableDecimal { Value = null }, "A16176F6", null, InteroperableOptions());
        }

        /// <summary>
        /// A decimal key goes out in the same form as a decimal value. It reaches the writer through
        /// the dictionary converter's key converter rather than as a member, which is also the path
        /// deterministic key ordering encodes keys on - so the bytes sorted are the bytes emitted.
        /// </summary>
        [Fact]
        public void WritesADecimalDictionaryKey()
        {
            Dictionary<decimal, int> dictionary = new Dictionary<decimal, int> { [273.15m] = 1 };

            // A1 map(1) C4 82 21 196AB3 01
            Helper.TestWrite(dictionary, "A1C48221196AB301", null, InteroperableOptions());
        }

        /// <summary>
        /// Deterministic key order is computed on the bytes that are written, not on a form chosen
        /// separately: these two keys sort one way as decimal fractions and the other way as
        /// DecimalFloat, and the emitted order follows the setting.
        /// </summary>
        /// <remarks>
        /// A decimal fraction leads with its exponent, so <c>5m</c> (<c>[0, 5]</c>) sorts before
        /// <c>0.3m</c> (<c>[-1, 3]</c>) - <c>00</c> before <c>20</c>. The DecimalFloat form leads with
        /// the mantissa words and puts the scale last, so it orders the same pair the other way round,
        /// mantissa 3 before mantissa 5.
        /// </remarks>
        [Fact]
        public void WritesDecimalDictionaryKeysInTheOrderOfTheFormItWrites()
        {
            Dictionary<decimal, int> dictionary = new Dictionary<decimal, int>
            {
                [5m] = 1,
                [0.3m] = 2,
            };

            CborOptions interoperable = InteroperableOptions();
            interoperable.Deterministic = true;

            // A2 map(2) C4820005 01 C4822003 02
            Helper.TestWrite(dictionary, "A2C482000501C482200302", null, interoperable);

            CborOptions legacy = new CborOptions { Deterministic = true };

            Helper.TestWrite(
                dictionary,
                "A2"
                + "FC00000000000000030000000000010000" + "02"
                + "FC00000000000000050000000000000000" + "01",
                null,
                legacy);
        }

        /// <summary>
        /// The object model has no decimal fraction of its own, so this is the one place the two
        /// directions do not meet: a <see cref="CborDecimal"/> writes as tag 4 like any other decimal,
        /// and reading those bytes back gives a tagged <see cref="CborArray"/> rather than a
        /// <see cref="CborDecimal"/>. The document is right; the DOM type is not what wrote it.
        /// </summary>
        [Fact]
        public void TheObjectModelWritesTagFourAndReadsAnArray()
        {
            CborValue value = 273.15m;

            Helper.TestWrite(value, "C48221196AB3", null, InteroperableOptions());

            CborValue read = Helper.Read<CborValue>("C48221196AB3");

            CborArray array = Assert.IsType<CborArray>(read);
            Assert.Equal(4ul, array.SemanticTag);
            Assert.Equal(2, array.Count);
            Assert.Equal(-2, array[0].Value<int>());
            Assert.Equal(27315, array[1].Value<int>());
        }

        [Fact]
        public void TheSettingReachesTheGeneratedOptions()
        {
            GeneratedDecimalFractionContext context =
                CborSerializerContext.Default<GeneratedDecimalFractionContext>();

            Assert.Equal(DecimalFormat.DecimalFraction, context.Options.DecimalFormat);
        }

        /// <summary>
        /// And the generated path writes the same bytes the reflection path does, on a member, on a
        /// nullable member and on a dictionary key alike.
        /// </summary>
        [Fact]
        public void TheGeneratedPathWritesWhatTheReflectionPathWrites()
        {
            GeneratedDecimalFractionContext context =
                CborSerializerContext.Default<GeneratedDecimalFractionContext>();

            GeneratedDecimalHolder value = new GeneratedDecimalHolder
            {
                Value = 273.15m,
                Optional = -0.5m,
                Keyed = new Dictionary<decimal, string> { [1.5m] = "one and a half" },
            };

            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value, InteroperableOptions()), generated);
            Assert.Contains("C48221196AB3", generated);
        }

        /// <summary>
        /// Every shape of <see cref="decimal"/> round-trips bit for bit, scale included: all 96
        /// mantissa bits, every scale, both signs. And what was read writes back byte-identically, so
        /// the encoding is a function of the value rather than of the route taken to it.
        /// </summary>
        /// <remarks>
        /// A fixed seed, so this is a large deterministic case list rather than a test that passes or
        /// fails depending on the day. It exists because the arithmetic has more corners than a hand
        /// written list covers: the reduction that brings a wide mantissa into range was originally
        /// driven by the scale alone, which refused values as ordinary as <c>1E+28</c>, and no vector
        /// anybody thought to write down caught it.
        /// </remarks>
        [Fact]
        public void EveryDecimalShapeRoundTripsBitForBit()
        {
            Random random = new Random(20260812);
            CborOptions options = InteroperableOptions();

            for (int i = 0; i < 20_000; i++)
            {
                int low = random.Next(int.MinValue, int.MaxValue);
                int middle = random.Next(3) == 0 ? random.Next(int.MinValue, int.MaxValue) : 0;
                int high = random.Next(4) == 0 ? random.Next(int.MinValue, int.MaxValue) : 0;
                bool isNegative = random.Next(2) == 0;
                byte scale = (byte)random.Next(29);

                decimal value = new decimal(low, middle, high, isNegative, scale);

                string hexBuffer = Helper.Write(value, options);
                decimal read = Helper.Read<decimal>(hexBuffer);

                int[] expected = decimal.GetBits(value);
                int[] actual = decimal.GetBits(read);

                // Negative zero is the documented exception: tag 4 has no signed zero, so the sign bit
                // is the one thing the round trip drops. See NegativeZeroLosesItsSign.
                if (isNegative && low == 0 && middle == 0 && high == 0)
                {
                    expected[3] &= 0x7FFFFFFF;
                }

                Assert.Equal(expected, actual);
                Assert.Equal(hexBuffer, Helper.Write(read, options));
            }
        }

        /// <summary>
        /// What the object model gives up is the node's type, not the document: a decimal fraction read
        /// into a <see cref="CborValue"/> writes back byte-identically, tag included. So a DOM read is
        /// still a faithful carrier of someone else's decimals, which is what makes the asymmetry above
        /// a limit of the model rather than a hole in it.
        /// </summary>
        [Theory]
        [InlineData("C48221196AB3")]                          // [-2, 27315]
        [InlineData("C48200C24CFFFFFFFFFFFFFFFFFFFFFFFF")]    // a mantissa needing the bignum tag
        // Past what decimal holds, and this is the case that argues against decoding tag 4 into a
        // CborDecimal here: the DOM carries a decimal fraction no .NET decimal can represent, and
        // narrowing it to the type would make this document unreadable rather than merely untyped.
        [InlineData("C48200C24D01000000000000000000000000")]  // [0, 2^96]
        public void TheObjectModelCarriesADecimalFractionWithoutHoldingIt(string hexBuffer)
        {
            CborValue read = Helper.Read<CborValue>(hexBuffer);

            Assert.Equal(hexBuffer, Helper.Write(read));
        }

        /// <summary>
        /// Negative zero is the one value the two forms disagree about. <c>decimal</c> has a sign bit
        /// independent of the mantissa; tag 4 does not, so <c>-0.00m</c> comes back as <c>0.00m</c> -
        /// equal by every comparison the language offers, and distinguishable only by
        /// <see cref="decimal.GetBits(decimal)"/> or by rendering it.
        /// </summary>
        [Fact]
        public void NegativeZeroLosesItsSign()
        {
            Helper.TestWrite(-0.00m, "C4822100", null, InteroperableOptions());

            decimal read = Helper.Read<decimal>("C4822100");

            Assert.Equal(-0.00m, read);
            Assert.Equal("0.00", read.ToString(CultureInfo.InvariantCulture));
        }
    }
}
