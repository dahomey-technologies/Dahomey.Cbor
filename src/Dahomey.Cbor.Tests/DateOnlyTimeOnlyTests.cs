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
        [InlineData(DateTimeFormat.ISO8601)]
        [InlineData(DateTimeFormat.Unix)]
        [InlineData(DateTimeFormat.UnixMilliseconds)]
        public void TimeOnlyHasOneEncodingWhateverTheDateTimeFormat(DateTimeFormat format)
        {
            // "01:02:03". DateTimeFormat chooses between representations of an instant, and a time of
            // day is not one.
            Helper.TestWrite(
                new TimeOnly(1, 2, 3), "6830313A30323A3033", null, new CborOptions { DateTimeFormat = format });
        }

        [Theory]
        [InlineData("190E8B")]               // 3723, seconds since midnight
        [InlineData("FA4568B000")]           // 3723.0
        [InlineData("1A00015180")]           // 86400
        [InlineData("20")]                   // -1
        [InlineData("FB7FF8000000000000")]   // NaN
        public void ANumberIsNotATimeOnly(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeOnly>(hexBuffer));
        }

        // The map below is what TimeOnly was written as before it had a converter: an object of its
        // public properties, captured by serializing with a plain CborOptions on the commit before
        // this converter existed. Documents stored in that form stay readable.
        [Theory]
        // 03:04:05 -- the old reader rebuilt it from Hour and Minute and returned 03:04:00; Ticks
        // is in the document, so nothing is lost on the way in.
        [InlineData(
            "A764486F757203664D696E75746504665365636F6E64056B4D696C6C697365636F6E64006B4D6963726F7365636F6E64006A4E616E6F7365636F6E6400655469636B731B00000019B7554080",
            3, 4, 5, 0)]
        // 03:04:05.678
        [InlineData(
            "A764486F757203664D696E75746504665365636F6E64056B4D696C6C697365636F6E641902A66B4D6963726F7365636F6E64006A4E616E6F7365636F6E6400655469636B731B00000019B7BCB4E0",
            3, 4, 5, 678)]
        public void ReadTimeOnlyFromTheMapEarlierVersionsWrote(
            string hexBuffer, int hours, int minutes, int seconds, int milliseconds)
        {
            Assert.Equal(new TimeOnly(hours, minutes, seconds, milliseconds), Helper.Read<TimeOnly>(hexBuffer));
        }

        [Fact]
        public void TheGeneratedPathReadsTheLegacyMapsToo()
        {
            // {"Date": {"Year": 2026, "Month": 8, "Day": 17}, "Time": <the 03:04:05 map above>, "OptionalDate": null}
            byte[] document = Convert.FromHexString(
                "A36444617465A364596561721907EA654D6F6E74680863446179116454696D65A764486F757203664D696E75746504665365636F6E64056B4D696C6C697365636F6E64006B4D6963726F7365636F6E64006A4E616E6F7365636F6E6400655469636B731B00000019B75540806C4F7074696F6E616C44617465F6");

            CborOptions options = CborSerializerContext.Default<DateContext>().Options;
            GeneratedDateHolder holder = Cbor.Deserialize<GeneratedDateHolder>(document, options);

            Assert.Equal(new DateOnly(2026, 8, 17), holder.Date);
            Assert.Equal(new TimeOnly(3, 4, 5), holder.Time);
            Assert.Null(holder.OptionalDate);
        }

        [Fact]
        public void TheLegacyTimeOnlyMapFallsBackToItsComponentsWithoutTicks()
        {
            // {"Hour": 3, "Minute": 4, "Second": 5}
            Assert.Equal(new TimeOnly(3, 4, 5), Helper.Read<TimeOnly>("A364486F757203664D696E75746504665365636F6E6405"));
        }

        [Fact]
        public void ALegacyTimeOnlyMapWithNoTimeInItIsRefused()
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeOnly>("A164486F757203"));   // {"Hour": 3}
        }

        [Fact]
        public void ReadTimeOnlyFromTicksAloneAndTicksWinOverTheComponents()
        {
            // {"Hour": 9, "Minute": 59, "Ticks": 37450000000} -- the components disagree with Ticks,
            // which carries the whole value and so decides: 01:02:25.
            Assert.Equal(
                new TimeOnly(1, 2, 25),
                Helper.Read<TimeOnly>("A364486F757209664D696E757465183B655469636B731B00000008B831A680"));
        }

        [Theory]
        [InlineData("A1655469636B731B000000C92A69C000")]   // {"Ticks": 864000000000}, the next midnight
        [InlineData("A1655469636B7320")]                   // {"Ticks": -1}
        public void ALegacyTimeOnlyMapWithTicksOutOfRangeIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<TimeOnly>(hexBuffer));
        }

        [Fact]
        public void LegacyKeysAreMatchedWithoutRegardToCase()
        {
            // {"year": 2026, "month": 8, "day": 17}, as a camel-case naming convention wrote it
            Assert.Equal(
                new DateOnly(2026, 8, 17),
                Helper.Read<DateOnly>("A364796561721907EA656D6F6E7468086364617911"));
        }

        [Fact]
        public void ReadDateOnlyFromDayNumberAlone()
        {
            // {"DayNumber": 739844}
            Assert.Equal(new DateOnly(2026, 8, 17), Helper.Read<DateOnly>("A1694461794E756D6265721A000B4A04"));
        }

        [Fact]
        public void ALegacyDateOnlyMapWithDayNumberOutOfRangeIsRefused()
        {
            // {"DayNumber": 3652059}, one past DateOnly.MaxValue
            Assert.Throws<CborException>(() => Helper.Read<DateOnly>("A1694461794E756D6265721A0037B9DB"));
        }

        // {"Year": 2026, "Year": 1999, "Month": 8, "Day": 17}
        private const string LegacyDateWithRepeatedYear =
            "A464596561721907EA64596561721907CF654D6F6E7468086344617911";

        // {"Ticks": 1, "Ticks": 37450000000}
        private const string LegacyTimeWithRepeatedTicks =
            "A2655469636B7301655469636B731B00000008B831A680";

        [Fact]
        public void ARepeatedKeyInALegacyMapIsRefusedByDefault()
        {
            CborException date = Assert.Throws<CborException>(
                () => Helper.Read<DateOnly>(LegacyDateWithRepeatedYear));
            Assert.Contains("Duplicate map key: Year", date.Message);

            CborException time = Assert.Throws<CborException>(
                () => Helper.Read<TimeOnly>(LegacyTimeWithRepeatedTicks));
            Assert.Contains("Duplicate map key: Ticks", time.Message);
        }

        [Fact]
        public void ARepeatedKeyInALegacyMapTakesTheLastUnderLastWins()
        {
            var options = new CborOptions { DuplicateKeyMode = DuplicateKeyMode.LastWins };

            Assert.Equal(new DateOnly(1999, 8, 17), Helper.Read<DateOnly>(LegacyDateWithRepeatedYear, options));
            Assert.Equal(new TimeOnly(1, 2, 25), Helper.Read<TimeOnly>(LegacyTimeWithRepeatedTicks, options));
        }

        public class DateAndTimeThenMore
        {
            public DateOnly Date { get; set; }
            public TimeOnly Time { get; set; }
            public string After { get; set; }
        }

        [Fact]
        public void ANonTextKeyInALegacyMapIsRefusedRatherThanLosingTheParentsPlace()
        {
            // {"Date": {"Year": 2026, "Month": 8, "Day": 17, 1: "x"}, "Time": "01:02:03", "After": "hello"}
            // The stray key used to be left unread, so the members after the date were misaligned and
            // came back as 00:00 and null without any error.
            Assert.Throws<CborException>(() => Helper.Read<DateAndTimeThenMore>(
                "A36444617465A464596561721907EA654D6F6E74680863446179110161786454696D656830313A30323A30336541667465726568656C6C6F"));
        }

        // DateOnly was written as an object of its public properties, which is the map below. Unlike
        // the TimeOnly map above these bytes are built from the BCL's member list rather than
        // captured: DateOnly's creator mapping does not resolve on net10.0, where the old code
        // throws, so there is no net10.0 capture to take.
        // {"Year": 2026, "Month": 8, "Day": 17, "DayOfWeek": 1, "DayOfYear": 229, "DayNumber": 739844}
        [Theory]
        [InlineData("A664596561721907EA654D6F6E7468086344617911694461794F665765656B01694461794F665965617218E5694461794E756D6265721A000B4A04")]
        // {"Year": 2026, "Month": 8, "Day": 17} -- DayNumber absent, so the components decide
        [InlineData("A364596561721907EA654D6F6E7468086344617911")]
        public void ReadDateOnlyFromTheMapEarlierVersionsWrote(string hexBuffer)
        {
            Assert.Equal(new DateOnly(2026, 8, 17), Helper.Read<DateOnly>(hexBuffer));
        }

        [Theory]
        [InlineData("A1654D6F6E746808")]   // {"Month": 8}
        public void ALegacyDateOnlyMapWithNoDateInItIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateOnly>(hexBuffer));
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
