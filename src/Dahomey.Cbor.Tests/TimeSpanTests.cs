using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedDurationHolder
    {
        public TimeSpan Elapsed { get; set; }
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// The setting has to be on the attribute, not on options handed to the base constructor.
    /// </summary>
    /// <remarks>
    /// Unlike every other wire-format option, this one decides which <em>mechanism</em> handles the
    /// type: under the default a TimeSpan is collected as an object, and under Duration it resolves to
    /// TimeSpanConverter. That choice is made while the context is generated, so a value supplied at
    /// run time arrives too late and the generated half would still carry the object mapping.
    /// </remarks>
    [CborSerializable(typeof(GeneratedDurationHolder))]
    [CborSourceGenerationOptions(TimeSpanFormat = TimeSpanFormat.Duration)]
    public partial class DurationContext : CborSerializerContext
    {
    }

    public class TimeSpanTests
    {
        private static CborOptions Duration()
        {
            return new CborOptions { TimeSpanFormat = TimeSpanFormat.Duration };
        }

        [Theory]
        // Tag 1002 over {1: seconds}, RFC 9581's duration. 3723 = 01:02:03.
        [InlineData("D903EAA101190E8B", 0, 1, 2, 3, 0)]
        [InlineData("D903EAA10100", 0, 0, 0, 0, 0)]
        [InlineData("D903EAA101390E0F", 0, -1, 0, 0, 0)]
        // A fractional part adds nanoseconds under key -9.
        [InlineData("D903EAA20101281A1DCD6500", 0, 0, 0, 1, 500)]
        // RFC 9581 3.3: the fraction is an unsigned count added to the base, so -1.5s is -2 + 0.5.
        // Hand-written: A2 {01: 21 (-2), 28 (-9): 1A 1DCD6500 (500000000)}.
        [InlineData("D903EAA20121281A1DCD6500", 0, 0, 0, -1, -500)]
        public void DurationRoundTrips(
            string hexBuffer, int days, int hours, int minutes, int seconds, int milliseconds)
        {
            TimeSpan expected = new TimeSpan(days, hours, minutes, seconds, milliseconds);

            Assert.Equal(expected, Helper.Read<TimeSpan>(hexBuffer, Duration()));
            Helper.TestWrite(expected, hexBuffer, null, Duration());
        }

        /// <summary>
        /// The finest value the type holds, so the nanosecond key is exercised at its own resolution
        /// rather than only at a round number of milliseconds.
        /// </summary>
        [Fact]
        public void OneTickIsAHundredNanoseconds()
        {
            Assert.Equal(TimeSpan.FromTicks(1), Helper.Read<TimeSpan>("D903EAA20100281864", Duration()));
            Helper.TestWrite(TimeSpan.FromTicks(1), "D903EAA20100281864", null, Duration());
        }

        [Theory]
        // RFC 9581 defines finer keys than this writes, and a peer may use them.
        [InlineData("D903EAA201002201", 10000)]      // key -3, one millisecond
        [InlineData("D903EAA201002501", 10)]         // key -6, one microsecond
        public void TheOtherFractionalKeysAreRead(string hexBuffer, long expectedTicks)
        {
            Assert.Equal(TimeSpan.FromTicks(expectedTicks), Helper.Read<TimeSpan>(hexBuffer, Duration()));
        }

        /// <summary>
        /// Not a form this writes, and not one RFC 9581 defines, but the obvious shape for a peer that
        /// never adopted the tag.
        /// </summary>
        [Fact]
        public void APlainNumberIsReadAsWholeSeconds()
        {
            Assert.Equal(TimeSpan.FromSeconds(3723), Helper.Read<TimeSpan>("190E8B", Duration()));
        }

        /// <summary>
        /// RFC 9581 3.4: key -1 is the timescale (1 = TAI), not a fraction. A length in SI seconds is
        /// the same on UTC and TAI. An unknown elective key (negative, e.g. clock class -2) is skipped.
        /// </summary>
        [Fact]
        public void TheTimescaleAndAnUnknownElectiveKeyAreAccepted()
        {
            // 1002({1: 3723, -1: 1})
            Assert.Equal(
                TimeSpan.FromSeconds(3723),
                Helper.Read<TimeSpan>("D903EAA201190E8B2001", Duration()));
            // 1002({1: 3723, -2: 5})
            Assert.Equal(
                TimeSpan.FromSeconds(3723),
                Helper.Read<TimeSpan>("D903EAA201190E8B2105", Duration()));
        }

        /// <summary>The same -1.5s through both signs of base: the sum is what counts, and only the floored form is written.</summary>
        [Fact]
        public void NegativeFractionalDurationsFollowTheRfc()
        {
            TimeSpan value = TimeSpan.FromSeconds(-1.5);

            Helper.TestWrite(value, "D903EAA20121281A1DCD6500", null, Duration());
            Assert.Equal(value, Helper.Read<TimeSpan>(Helper.Write(value, Duration()), Duration()));
            // -0.25s: base -1, fraction 750000000 (hand-written).
            Helper.TestWrite(TimeSpan.FromSeconds(-0.25), "D903EAA20120281A2CB41780", null, Duration());
            // -1 + 0.5 = -0.5s read from a hand-written payload.
            Assert.Equal(TimeSpan.FromSeconds(-0.5), Helper.Read<TimeSpan>("D903EAA20120281A1DCD6500", Duration()));
        }

        [Theory]
        [InlineData(long.MinValue)]
        [InlineData(long.MaxValue)]
        [InlineData(-1L)]
        [InlineData(-9999999L)]
        [InlineData(0L)]
        public void TickExtremesRoundTrip(long ticks)
        {
            TimeSpan value = TimeSpan.FromTicks(ticks);
            Assert.Equal(value, Helper.Read<TimeSpan>(Helper.Write(value, Duration()), Duration()));
        }

        [Theory]
        // negative fraction (-9: -1)
        [InlineData("D903EAA201002820")]
        // two fraction keys: -3 and -9
        [InlineData("D903EAA3010022012801")]
        // base under key 4 (decimal fraction)
        [InlineData("D903EAA104822101")]
        // sub-tick fraction, 1ns under key -9
        [InlineData("D903EAA201002801")]
        // out of range: 2^62 seconds
        [InlineData("D903EAA1011B4000000000000000")]
        // seconds + fraction past MaxValue: 922337203685 s + 0.6 s
        [InlineData("D903EAA2011B000000D6BF94D5E5281A23C34600")]
        // missing base
        [InlineData("D903EAA1281A1DCD6500")]
        // unsupported timescale 2
        [InlineData("D903EAA201012002")]
        // unknown critical key 7
        [InlineData("D903EAA201000700")]
        // float base together with a fraction
        [InlineData("D903EAA201F93E002801")]
        public void AnUnreadableDurationIsACborException(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeSpan>(hexBuffer, Duration()));
        }

        /// <summary>
        /// RFC 9581 section 3: a text-string key, and a negative-integer key the reader does not
        /// understand, are elective and ignored -- including one outside the range of an int.
        /// </summary>
        [Theory]
        [InlineData("D903EAA20105617801")]                          // "x": 1
        [InlineData("D903EAA201056178" + "82" + "01A1617902")]      // "x": [1, {"y": 2}]
        [InlineData("D903EAA201056178A10102")]    // "x": {1: 2}
        [InlineData("D903EAA201053B000000FFFFFFFFFF01")]            // -2^40 - 1: 1
        [InlineData("D903EAA201053BFFFFFFFFFFFFFFFF01")]            // -2^64: 1, beyond a long
        [InlineData("D903EAA201053A8000000082" + "0102")]           // -2^31 - 1: [1, 2]
        public void ATextOrUnknownNegativeKeyIsIgnored(string hexBuffer)
        {
            Assert.Equal(TimeSpan.FromSeconds(5), Helper.Read<TimeSpan>(hexBuffer, Duration()));
        }

        [Theory]
        [InlineData("D903EAA201051A8000000001")]                    // key 2^31, a critical key
        [InlineData("D903EAA201051B000000010000000001")]            // key 2^32
        [InlineData("D903EAA20105" + "1BFFFFFFFFFFFFFFFF01")]       // key 2^64 - 1
        [InlineData("D903EAA2010541" + "7801")]                     // a byte-string key
        public void AnUnknownPositiveOrMalformedKeyIsACborException(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeSpan>(hexBuffer, Duration()));
        }

        /// <summary>
        /// A fraction of a second or more beside a negative base: the total is in range, so it reads,
        /// whichever way the same instant is split between base and fraction.
        /// </summary>
        [Fact]
        public void AFractionOfSeveralSecondsBesideANegativeBaseNearTheLimitIsRead()
        {
            // {1: -922337203690, -3: 5000} -- 5000 ms -- is -922337203685 s
            TimeSpan split = Helper.Read<TimeSpan>("D903EAA2013B000000D6BF94D5E922191388", Duration());
            // {1: -922337203685}
            TimeSpan whole = Helper.Read<TimeSpan>("D903EAA1013B000000D6BF94D5E4", Duration());

            Assert.Equal(whole, split);
            Assert.Equal(-922337203685L * TimeSpan.TicksPerSecond, split.Ticks);
        }

        [Theory]
        [InlineData("D903EAA1013B000000D6BF94D5E9")]                // -922337203690 s
        [InlineData("D903EAA2013B000000D6BF94D5E922190BB8")]        // -922337203690 s + 3000 ms
        [InlineData("D903EAA2011B000000D6BF94D5E522191388")]        // 922337203685 s + 5000 ms
        public void AnOutOfRangeSumIsACborException(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeSpan>(hexBuffer, Duration()));
        }

        [Fact]
        public void AFinerFractionIsAcceptedWhenItIsAWholeNumberOfTicks()
        {
            // -9: 200 ns = 2 ticks
            Assert.Equal(TimeSpan.FromTicks(2), Helper.Read<TimeSpan>("D903EAA2010028" + "18C8", Duration()));
            // -12: 100000 ps = 100 ns = 1 tick
            Assert.Equal(TimeSpan.FromTicks(1), Helper.Read<TimeSpan>("D903EAA201002B1A000186A0", Duration()));
        }

        [Fact]
        public void AFloatBaseIsRead()
        {
            // 1002({1: 1.5})
            Assert.Equal(TimeSpan.FromSeconds(1.5), Helper.Read<TimeSpan>("D903EAA101F93E00", Duration()));
        }

        /// <summary>
        /// The default is the historical encoding, and this is what keeps the option from being a wire
        /// break: without asking for the duration form a TimeSpan is still the object mapping's map of
        /// public properties, which is what every document written so far contains.
        /// </summary>
        [Fact]
        public void TheDefaultIsUnchangedAndIsNotTheDurationTag()
        {
            string written = Helper.Write(new TimeSpan(1, 2, 3));

            Assert.DoesNotContain("D903EA", written);
            Assert.StartsWith("B0", written);   // a map of 16 members, as before
            Assert.Equal(new TimeSpan(1, 2, 3), Helper.Read<TimeSpan>(written));
        }

        /// <summary>
        /// The two forms are not interchangeable, which the option's own documentation states: there is
        /// no converter under the default, so nothing reads tag 1002 there.
        /// </summary>
        [Fact]
        public void TheDefaultDoesNotReadADuration()
        {
            Assert.ThrowsAny<Exception>(() => Helper.Read<TimeSpan>("D903EAA101190E8B"));
        }

        [Fact]
        public void RoundTripsThroughAPocoMember()
        {
            GeneratedDurationHolder value = new GeneratedDurationHolder
            {
                Elapsed = new TimeSpan(0, 1, 2, 3, 500),
                Timeout = TimeSpan.FromSeconds(-30),
            };

            GeneratedDurationHolder read =
                Helper.Read<GeneratedDurationHolder>(Helper.Write(value, Duration()), Duration());

            Assert.Equal(value.Elapsed, read.Elapsed);
            Assert.Equal(value.Timeout, read.Timeout);
        }

        /// <summary>
        /// 259 bytes against 8 for the same value, which is the practical reason to opt in.
        /// </summary>
        [Fact]
        public void TheDurationFormIsFarSmaller()
        {
            int members = Helper.Write(new TimeSpan(1, 2, 3)).Length / 2;
            int duration = Helper.Write(new TimeSpan(1, 2, 3), Duration()).Length / 2;

            Assert.Equal(259, members);
            Assert.Equal(8, duration);
        }
    }
}
