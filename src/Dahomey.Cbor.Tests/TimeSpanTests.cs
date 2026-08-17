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
        // Both components go negative together, so the value is their sum in every case.
        [InlineData("D903EAA20120283A1DCD64FF", 0, 0, 0, -1, -500)]
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
        /// RFC 9581 §3 defines keys this converter does not act on -- a timescale, a clock quality --
        /// and none of them change the length being described, so an unrecognised one is skipped rather
        /// than failing a document that is otherwise readable.
        /// </summary>
        [Fact]
        public void AnUnrecognisedKeyIsSkipped()
        {
            // 1002({1: 3723, -1: 1}) -- key -1 is the timescale indicator.
            Assert.Equal(
                TimeSpan.FromSeconds(3723),
                Helper.Read<TimeSpan>("D903EAA201190E8B2001", Duration()));
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
