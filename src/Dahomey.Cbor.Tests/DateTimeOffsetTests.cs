using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedOffsetHolder
    {
        public DateTimeOffset Stamp { get; set; }
        public DateTimeOffset? Optional { get; set; }
    }

    [CborSerializable(typeof(GeneratedOffsetHolder))]
    public partial class OffsetContext : CborSerializerContext
    {
    }

    public class DateTimeOffsetTests
    {
        private static DateTimeOffset At(int hours, int minutes = 0)
        {
            return new DateTimeOffset(2026, 8, 17, 1, 2, 3, new TimeSpan(hours, minutes, 0));
        }

        [Theory]
        [InlineData("C07819323032362D30382D31375430313A30323A30332B30323A3030", 2)]
        [InlineData("C07819323032362D30382D31375430313A30323A30332D30383A3030", -8)]
        // A zero offset writes as "+00:00" rather than "Z": on a DateTimeOffset the offset is a value
        // in its own right, and "K" renders it numerically. Both forms are RFC 3339 and both read back
        // as the same instant at the same offset.
        [InlineData("C07819323032362D30382D31375430313A30323A30332B30303A3030", 0)]
        public void WriteKeepsTheOffset(string hexBuffer, int offsetHours)
        {
            Helper.TestWrite(At(offsetHours), hexBuffer);
        }

        [Fact]
        public void WriteKeepsAMinutesBearingOffset()
        {
            Helper.TestWrite(At(5, 30), "C07819323032362D30382D31375430313A30323A30332B30353A3330");
        }

        [Theory]
        [InlineData("C07819323032362D30382D31375430313A30323A30332B30323A3030", 2)]
        [InlineData("C07819323032362D30382D31375430313A30323A30332D30383A3030", -8)]
        [InlineData("C07819323032362D30382D31375430313A30323A30332B30303A3030", 0)]
        // "Z" is an offset -- zero, stated -- and reads as one.
        [InlineData("C074323032362D30382D31375430313A30323A30335A", 0)]
        public void ReadKeepsTheOffset(string hexBuffer, int offsetHours)
        {
            DateTimeOffset value = Helper.Read<DateTimeOffset>(hexBuffer);

            Assert.Equal(At(offsetHours), value);
            Assert.Equal(TimeSpan.FromHours(offsetHours), value.Offset);
        }

        /// <summary>
        /// The distinction the whole type exists for: two documents naming the same instant at
        /// different offsets are equal as instants and are not interchangeable as values.
        /// </summary>
        [Fact]
        public void TwoOffsetsNamingOneInstantAreEqualButNotIdentical()
        {
            DateTimeOffset berlin =
                Helper.Read<DateTimeOffset>("C07819323032362D30382D31375430313A30323A30332B30323A3030");
            DateTimeOffset utc =
                Helper.Read<DateTimeOffset>("C074323032362D30382D31365432333A30323A30335A");

            Assert.Equal(berlin, utc);
            Assert.NotEqual(berlin.Offset, utc.Offset);
            Assert.False(berlin.EqualsExact(utc));
        }

        [Fact]
        public void FractionalSecondsSurvive()
        {
            const string hex = "C0781D323032362D30382D31375430313A30323A30332E3834312B30323A3030";

            DateTimeOffset value = Helper.Read<DateTimeOffset>(hex);

            Assert.Equal(841, value.Millisecond);
            Assert.Equal(TimeSpan.FromHours(2), value.Offset);
            Helper.TestWrite(value, hex);
        }

        /// <summary>
        /// RFC 3339 section 5.6's own worked example, which crosses midnight and a month boundary.
        /// </summary>
        [Fact]
        public void Rfc3339SectionFiveSixExample()
        {
            DateTimeOffset value =
                Helper.Read<DateTimeOffset>("C07819313939362D31322D31395431363A33393A35372D30383A3030");

            Assert.Equal(new DateTime(1996, 12, 20, 0, 39, 57, DateTimeKind.Utc), value.UtcDateTime);
            Assert.Equal(TimeSpan.FromHours(-8), value.Offset);
        }

        /// <summary>
        /// Tag 1 is a count of seconds since the epoch, so it names an instant and has nowhere to put
        /// an offset. Both numeric formats therefore write the right moment and lose the offset, which
        /// is a property of the encoding rather than of this converter.
        /// </summary>
        [Theory]
        [InlineData("C11A6A82416B", DateTimeFormat.Unix)]
        [InlineData("C1FB41DAA0905AC00000", DateTimeFormat.UnixMilliseconds)]
        public void ANumericFormatKeepsTheInstantAndDropsTheOffset(string hexBuffer, DateTimeFormat format)
        {
            CborOptions options = new CborOptions { DateTimeFormat = format };

            Helper.TestWrite(At(2), hexBuffer, null, options);

            DateTimeOffset read = Helper.Read<DateTimeOffset>(hexBuffer, options);

            Assert.Equal(At(2), read);                    // the same instant
            Assert.Equal(TimeSpan.Zero, read.Offset);     // at a different offset
        }

        [Theory]
        [InlineData("C07819323032362D31332D31375430313A30323A30332B30323A3030")]  // month 13
        [InlineData("C07819323032362D30382D31375430313A30323A30332B31353A3030")]  // beyond 14 hours
        [InlineData("C07819323032362D30382D31375430313A30323A30332B30323A3939")]  // 99 minutes
        public void MalformedInputIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateTimeOffset>(hexBuffer));
        }

        /// <summary>
        /// A numeric time stamp outside what a <see cref="DateTimeOffset"/> holds is a format error like
        /// the string form's, not an <see cref="ArgumentOutOfRangeException"/> from the BCL.
        /// </summary>
        [Theory]
        [InlineData("C11B7FFFFFFFFFFFFFFF")]  // long.MaxValue seconds
        [InlineData("C11BFFFFFFFFFFFFFFFF")]  // beyond long.MaxValue
        [InlineData("C13B7FFFFFFFFFFFFFFF")]  // long.MinValue seconds
        [InlineData("C11B0000003AFFF44180")]  // 253402300800, one second past DateTimeOffset.MaxValue
        [InlineData("C13B0000000E7791F700")]  // -62135596801, one second before DateTimeOffset.MinValue
        [InlineData("C1FB4415AF1D78B58C40")]  // the double 1e20
        [InlineData("C1FBC415AF1D78B58C40")]  // the double -1e20
        [InlineData("C1FA7F7FFFFF")]          // float.MaxValue
        public void ANumericTimeStampOutOfRangeIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateTimeOffset>(hexBuffer));
        }

        /// <summary>
        /// NaN compares false against every bound, so without an explicit check it reads as the epoch.
        /// </summary>
        [Theory]
        [InlineData("C1F97E00")]              // NaN, half
        [InlineData("C1FA7FC00000")]          // NaN, single
        [InlineData("C1FB7FF8000000000000")]  // NaN, double
        [InlineData("C1F97C00")]              // +Infinity
        [InlineData("C1F9FC00")]              // -Infinity
        [InlineData("C1FA7F800000")]          // +Infinity, single
        [InlineData("C1FB7FF0000000000000")]  // +Infinity, double
        [InlineData("C1FBFFF0000000000000")]  // -Infinity, double
        public void ANonFiniteTimeStampIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateTimeOffset>(hexBuffer));
        }

        /// <summary>The bounds themselves are representable and must still read.</summary>
        [Fact]
        public void TheBoundsOfTheRangeStillRead()
        {
            // 253402300799 seconds is 9999-12-31T23:59:59Z and -62135596800 is 0001-01-01T00:00:00Z.
            Assert.Equal(
                new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero),
                Helper.Read<DateTimeOffset>("C11B0000003AFFF4417F"));
            Assert.Equal(
                DateTimeOffset.MinValue,
                Helper.Read<DateTimeOffset>("C13B0000000E7791F6FF"));

            // The same two bounds as doubles, which are exact at this magnitude.
            Assert.Equal(
                new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero),
                Helper.Read<DateTimeOffset>("C1FB424D7FFA20BF8000"));
            Assert.Equal(
                DateTimeOffset.MinValue,
                Helper.Read<DateTimeOffset>("C1FBC22CEF23EE000000"));
        }

        /// <summary>
        /// A string naming no offset is given one by <c>UnqualifiedTimeZoneDateTimeKind</c>, exactly as
        /// constructing a <see cref="DateTimeOffset"/> from a <see cref="DateTime"/> of that kind does.
        /// </summary>
        [Fact]
        public void AnUnqualifiedStringTakesTheOffsetTheOptionsAsk()
        {
            const string hex = "C073323032362D30382D31375430313A30323A3033";

            DateTimeOffset utc = Helper.Read<DateTimeOffset>(
                hex, new CborOptions { UnqualifiedTimeZoneDateTimeKind = DateTimeKind.Utc });

            Assert.Equal(TimeSpan.Zero, utc.Offset);
            Assert.Equal(new DateTime(2026, 8, 17, 1, 2, 3), utc.DateTime);
        }

        /// <summary>
        /// Before this converter existed the reflection path mapped the struct over its public
        /// properties: 807 bytes that read back as <c>default</c>, silently.
        /// </summary>
        [Fact]
        public void RoundTripsThroughAPocoMember()
        {
            GeneratedOffsetHolder value = new GeneratedOffsetHolder
            {
                Stamp = At(2),
                Optional = At(-8),
            };

            GeneratedOffsetHolder read = Helper.Read<GeneratedOffsetHolder>(Helper.Write(value));

            Assert.Equal(value.Stamp, read.Stamp);
            Assert.Equal(value.Stamp.Offset, read.Stamp.Offset);
            Assert.Equal(value.Optional, read.Optional);
        }
    }
}
