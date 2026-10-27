using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Every input that cannot be turned into a DateTime is a CborException carrying the path, as it
    /// is for a DateTimeOffset. The BCL's ArgumentOutOfRangeException would not match a caller's
    /// catch (CborException) and reports no path.
    /// </summary>
    public class DateTimeRangeTests
    {
        public class Holder
        {
            public DateTime When { get; set; }
        }

        public class OffsetHolder
        {
            public DateTimeOffset When { get; set; }
        }

        private static byte[] TaggedText(string text, bool tagged = true)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            byte[] header = bytes.Length < 24
                ? new byte[] { (byte)(0x60 + bytes.Length) }
                : new byte[] { 0x78, (byte)bytes.Length };

            return (tagged ? new byte[] { 0xC0 } : new byte[0]).Concat(header).Concat(bytes).ToArray();
        }

        private static byte[] AsMember(byte[] value)
        {
            return new byte[] { 0xA1, 0x64, (byte)'W', (byte)'h', (byte)'e', (byte)'n' }.Concat(value).ToArray();
        }

        private static byte[] Hex(string hex) => Convert.FromHexString(hex);

        private static CborException Refusal<T>(byte[] buffer)
        {
            try
            {
                Cbor.Deserialize<T>(buffer);
            }
            catch (CborException e)
            {
                return e;
            }

            return null;
        }

        public static readonly object[][] BadText =
        {
            new object[] { "0001-01-01T00:00:00+01:00" },  // the offset moves it below DateTime.MinValue
            new object[] { "9999-12-31T23:59:59-01:00" },  // the offset moves it above DateTime.MaxValue
            new object[] { "0000-01-01T00:00:00Z" },       // year 0
            new object[] { "2020-02-30T00:00:00Z" },       // February 30
            new object[] { "2020-02-30T00:00:00" },        // the same without an offset
            new object[] { "2020-13-01T00:00:00Z" },       // month 13
            new object[] { "2020-01-01T24:00:00Z" },       // hour 24
            new object[] { "2020-01-01T00:61:00Z" },       // minute 61
            new object[] { "2016-12-31T23:59:60Z" },       // a leap second stays refused
        };

        public static readonly object[][] BadNumbers =
        {
            new object[] { "C11B0000003AFFF44180" },       // 253402300800, one second past the range
            new object[] { "C13B0000000E7791F700" },       // -62135596801, one second before it
            new object[] { "C1FB4415AF1D78B58C40" },       // the double 1e20
            new object[] { "C1FBC415AF1D78B58C40" },       // the double -1e20
            new object[] { "C1F97C00" },                   // half-float +Infinity
            new object[] { "C1F9FC00" },                   // half-float -Infinity
            new object[] { "C1FA7F800000" },               // single +Infinity
        };

        [Theory]
        [MemberData(nameof(BadText))]
        public void ATextDateTheCalendarRefusesIsACborExceptionAtTheRoot(string text)
        {
            CborException exception = Refusal<DateTime>(TaggedText(text));

            Assert.NotNull(exception);
            Assert.Contains("Invalid date format", exception.Message);
            Assert.Contains(text, exception.Message);
        }

        [Theory]
        [MemberData(nameof(BadText))]
        public void ATextDateTheCalendarRefusesIsACborExceptionWithItsPathAsAMember(string text)
        {
            CborException exception = Refusal<Holder>(AsMember(TaggedText(text)));

            Assert.NotNull(exception);
            Assert.Contains("Invalid date format", exception.Message);
            Assert.Equal("$.When", exception.Path);
        }

        [Theory]
        [MemberData(nameof(BadText))]
        public void AnUntaggedTextStringTakesTheSameRoute(string text)
        {
            Assert.NotNull(Refusal<DateTime>(TaggedText(text, tagged: false)));
        }

        [Theory]
        [MemberData(nameof(BadNumbers))]
        public void ANumericTimeStampOutOfRangeIsACborExceptionAtTheRoot(string hex)
        {
            CborException exception = Refusal<DateTime>(Hex(hex));

            Assert.NotNull(exception);
            Assert.Contains("Invalid date format", exception.Message);
        }

        [Theory]
        [MemberData(nameof(BadNumbers))]
        public void ANumericTimeStampOutOfRangeIsACborExceptionWithItsPathAsAMember(string hex)
        {
            CborException exception = Refusal<Holder>(AsMember(Hex(hex)));

            Assert.NotNull(exception);
            Assert.Equal("$.When", exception.Path);
        }

        /// <summary>
        /// The two converters refuse the same inputs, with the same exception type, the same path and
        /// a message that differs only in the name of the type the range belongs to.
        /// </summary>
        [Theory]
        [MemberData(nameof(BadText))]
        public void ATextDateIsRefusedAsADateTimeOffsetIs(string text)
        {
            AssertSameRefusal(AsMember(TaggedText(text)));
        }

        [Theory]
        [MemberData(nameof(BadNumbers))]
        public void ANumericTimeStampIsRefusedAsADateTimeOffsetIs(string hex)
        {
            AssertSameRefusal(AsMember(Hex(hex)));
        }

        private static void AssertSameRefusal(byte[] buffer)
        {
            CborException dateTime = Refusal<Holder>(buffer);
            CborException dateTimeOffset = Refusal<OffsetHolder>(buffer);

            Assert.NotNull(dateTime);
            Assert.NotNull(dateTimeOffset);
            Assert.Equal(dateTimeOffset.Path, dateTime.Path);
            Assert.Equal(
                dateTimeOffset.Message.Replace("DateTimeOffset holds", "DateTime holds"),
                dateTime.Message);
        }

        [Fact]
        public void ACatchOfCborExceptionCatchesIt()
        {
            bool caught = false;

            try
            {
                Cbor.Deserialize<DateTime>(TaggedText("2020-02-30T00:00:00Z"));
            }
            catch (CborException)
            {
                caught = true;
            }

            Assert.True(caught);
        }

        [Theory]
        [InlineData("2020-01-02T03:04:05Z", 2020, 1, 2, 3, 4, 5, 0L)]
        [InlineData("0001-01-01T00:00:00Z", 1, 1, 1, 0, 0, 0, 0L)]
        [InlineData("9999-12-31T23:59:59Z", 9999, 12, 31, 23, 59, 59, 0L)]
        [InlineData("2020-01-02T03:04:05.5Z", 2020, 1, 2, 3, 4, 5, 5000000L)]
        [InlineData("2020-01-02T03:04:05.1234567Z", 2020, 1, 2, 3, 4, 5, 1234567L)]
        [InlineData("2020-01-02T05:04:05+02:00", 2020, 1, 2, 3, 4, 5, 0L)]
        [InlineData("0001-01-01T01:00:00+01:00", 1, 1, 1, 0, 0, 0, 0L)]  // the offset lands exactly on the bound
        [InlineData("9999-12-31T22:59:59-01:00", 9999, 12, 31, 23, 59, 59, 0L)]
        public void InRangeTextStillReads(
            string text, int year, int month, int day, int hour, int minute, int second, long ticks)
        {
            DateTime expected = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).AddTicks(ticks);

            Assert.Equal(expected, Cbor.Deserialize<DateTime>(TaggedText(text)));
        }

        [Fact]
        public void TheBoundsOfTheNumericRangeStillRead()
        {
            Assert.Equal(
                new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                Cbor.Deserialize<DateTime>(Hex("C11B0000003AFFF4417F")));
            Assert.Equal(
                DateTime.MinValue,
                Cbor.Deserialize<DateTime>(Hex("C13B0000000E7791F6FF")));
        }
    }
}
