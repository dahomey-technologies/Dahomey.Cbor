using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedDateHolder
    {
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public DateOnly? OptionalDate { get; set; }
    }

    /// <summary>
    /// Exercises the source-generated path, which a run-time test cannot reach.
    /// </summary>
    /// <remarks>
    /// The regression this guards is silent in the same way the RFC 8949 section 3.4.4 structs are.
    /// Both are non-generic structs, so without an entry in <c>TypeCollector.IsPrimitive</c>
    /// <c>Classify</c> falls through to <c>TypeKind.Object</c> and the emitted context writes their
    /// public properties as a map -- <c>Year</c>, <c>Month</c>, <c>DayNumber</c> and the rest -- which
    /// builds green and produces the wrong bytes. What catches it is the <c>GeneratedCorpusTests</c>
    /// comparison against the reflection path.
    /// </remarks>
    [CborSerializable(typeof(GeneratedDateHolder))]
    public partial class DateContext : CborSerializerContext
    {
    }

    public class DateOnlyTimeOnlyTests
    {
        [Theory]
        // Tag 1004 over an RFC 3339 full-date string, which is what RFC 8943 registers it for.
        [InlineData("D903EC6A323032362D30382D3137", 2026, 8, 17, DateTimeFormat.ISO8601)]
        [InlineData("D903EC6A313937302D30312D3031", 1970, 1, 1, DateTimeFormat.ISO8601)]
        // Tag 100 over days since 1970-01-01, RFC 8943's other registered encoding. 20682 days.
        [InlineData("D8641950CA", 2026, 8, 17, DateTimeFormat.Unix)]
        [InlineData("D86400", 1970, 1, 1, DateTimeFormat.Unix)]
        // A date before the epoch is a negative count, which the tag's own definition allows.
        [InlineData("D86438B4", 1969, 7, 4, DateTimeFormat.Unix)]
        public void WriteDateOnly(string hexBuffer, int year, int month, int day, DateTimeFormat format)
        {
            Helper.TestWrite(
                new DateOnly(year, month, day), hexBuffer, null, new CborOptions { DateTimeFormat = format });
        }

        [Theory]
        [InlineData("D903EC6A323032362D30382D3137", 2026, 8, 17)]
        [InlineData("D8641950CA", 2026, 8, 17)]
        // Either encoding is read whatever DateTimeFormat is set to: that option describes what this
        // end writes, and a peer's choice is not ours to make.
        [InlineData("6A323032362D30382D3137", 2026, 8, 17)]
        [InlineData("1950CA", 2026, 8, 17)]
        // A leap day, which is the one date a wrong month-length table gets wrong.
        [InlineData("D903EC6A323032342D30322D3239", 2024, 2, 29)]
        public void ReadDateOnly(string hexBuffer, int year, int month, int day)
        {
            Assert.Equal(new DateOnly(year, month, day), Helper.Read<DateOnly>(hexBuffer));
        }

        [Theory]
        [InlineData("6830313A30323A3033")]           // "01:02:03"
        [InlineData("6C30313A30323A30332E383431")]   // "01:02:03.841"
        [InlineData("7032333A35393A35392E39393939393939")]  // "23:59:59.9999999", the largest there is
        [InlineData("6830303A30303A3030")]           // "00:00:00", midnight, whose ticks are zero
        public void TimeOnlyRoundTrips(string hexBuffer)
        {
            TimeOnly value = Helper.Read<TimeOnly>(hexBuffer);

            // Written untagged: the CBOR tag registry has nothing for a time of day, and occupying an
            // unassigned number would produce documents another decoder may reject.
            Helper.TestWrite(value, hexBuffer);
        }

        [Theory]
        [InlineData("6830313A30323A3033", 1, 2, 3, 0)]
        [InlineData("6C30313A30323A30332E383431", 1, 2, 3, 841)]
        // A fraction finer than 100ns is truncated rather than refused -- a peer with a more precise
        // clock is interoperating correctly, and TimeOnly has nowhere to put the rest.
        [InlineData("7232333A35393A35392E393939393939393939", 23, 59, 59, 999)]
        public void ReadTimeOnly(string hexBuffer, int hours, int minutes, int seconds, int milliseconds)
        {
            TimeOnly value = Helper.Read<TimeOnly>(hexBuffer);

            Assert.Equal(hours, value.Hour);
            Assert.Equal(minutes, value.Minute);
            Assert.Equal(seconds, value.Second);
            Assert.Equal(milliseconds, value.Millisecond);
        }

        [Theory]
        // Seconds since midnight, under both numeric formats. 3723 = 01:02:03.
        [InlineData("190E8B", 1, 2, 3)]
        [InlineData("00", 0, 0, 0)]
        [InlineData("FB40AD160000000000", 1, 2, 3)]
        public void ReadTimeOnlyFromSecondsSinceMidnight(string hexBuffer, int hours, int minutes, int seconds)
        {
            Assert.Equal(new TimeOnly(hours, minutes, seconds), Helper.Read<TimeOnly>(hexBuffer));
        }

        [Theory]
        [InlineData("190E8B", DateTimeFormat.Unix)]
        [InlineData("FA4568B000", DateTimeFormat.UnixMilliseconds)]  // narrowed to a single, which holds 3723 exactly
        public void WriteTimeOnlyAsSecondsSinceMidnight(string hexBuffer, DateTimeFormat format)
        {
            Helper.TestWrite(
                new TimeOnly(1, 2, 3), hexBuffer, null, new CborOptions { DateTimeFormat = format });
        }

        [Theory]
        [InlineData("6A323032362D31332D3137")]   // month 13
        [InlineData("6A323032352D30322D3239")]   // 2025 is not a leap year
        [InlineData("6A323032362D30382D3030")]   // day 0
        [InlineData("69323032362D382D3137")]     // an unpadded month, which full-date does not admit
        [InlineData("6B323032362D30382D31375A")] // a trailing designator
        public void MalformedDateIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateOnly>(hexBuffer));
        }

        [Theory]
        [InlineData("6832343A30303A3030")]       // 24:00:00 is a legal instant but not a time of day
        [InlineData("6830313A36303A3033")]       // minute 60
        [InlineData("6830313A30323A3630")]       // a leap second, which TimeOnly has no room for
        [InlineData("6930313A30323A30332E")]     // a separator with no fraction after it
        [InlineData("6730313A30323A33")]         // an unpadded second
        public void MalformedTimeIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeOnly>(hexBuffer));
        }

        [Theory]
        [InlineData("1A7FFFFFFF")]               // a day count far past DateOnly.MaxValue
        [InlineData("3A7FFFFFFF")]               // and far before its minimum
        public void OutOfRangeDayCountIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateOnly>(hexBuffer));
        }

        [Theory]
        [InlineData("1A00015180")]             // 86400 seconds is the next midnight, not a time
        [InlineData("20")]                       // -1
        [InlineData("FB7FF8000000000000")]       // NaN, which fails every range comparison
        public void OutOfRangeSecondsAreRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeOnly>(hexBuffer));
        }

        /// <summary>
        /// The whole reason these converters exist: without them the reflection path maps each type as
        /// an object over its public properties, which throws for one and silently drops the seconds of
        /// the other.
        /// </summary>
        [Fact]
        public void BothTypesRoundTripThroughAPocoMember()
        {
            Container value = new Container
            {
                Date = new DateOnly(2026, 8, 17),
                Time = new TimeOnly(1, 2, 3, 841),
            };

            Container read = Helper.Read<Container>(Helper.Write(value));

            Assert.Equal(value.Date, read.Date);
            Assert.Equal(value.Time, read.Time);
        }

        public class Container
        {
            public DateOnly Date { get; set; }
            public TimeOnly Time { get; set; }
        }
    }
}
