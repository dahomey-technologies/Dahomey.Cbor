using System;
using System.Text;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    // RFC 3339 section 5.6: time-secfrac = "." 1*DIGIT, so a fraction is a decimal fraction of a
    // second, and ".5" is half a second whatever the number of digits written.
    public class DateTimeFractionTests
    {
        private static string TextHex(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            Assert.InRange(bytes.Length, 0, 255);
            string header = bytes.Length < 24 ? (0x60 + bytes.Length).ToString("X2") : "78" + bytes.Length.ToString("X2");
            return header + Convert.ToHexString(bytes);
        }

        private static DateTime ReadText(string text) => Helper.Read<DateTime>(TextHex(text));

        private static readonly DateTime Base = new DateTime(2010, 5, 25, 11, 9, 46, DateTimeKind.Utc);

        [Theory]
        [InlineData("2010-05-25T11:09:46.5Z", 5000000L)]
        [InlineData("2010-05-25T11:09:46.05Z", 500000L)]
        [InlineData("2010-05-25T11:09:46.50Z", 5000000L)]
        [InlineData("2010-05-25T11:09:46.123Z", 1230000L)]
        [InlineData("2010-05-25T11:09:46.123456Z", 1234560L)]
        [InlineData("2010-05-25T11:09:46.1234567Z", 1234567L)]
        [InlineData("2010-05-25T11:09:46.000000001Z", 0L)]   // beyond 100 ns, truncated
        [InlineData("2010-05-25T11:09:46.123456789Z", 1234567L)] // truncated, not rounded
        [InlineData("2010-05-25T11:09:46.999999999Z", 9999999L)] // truncation never carries into the next second
        [InlineData("2010-05-25T11:09:46.0Z", 0L)]
        public void ReadFraction(string text, long expectedTicks)
        {
            DateTime actual = ReadText(text);
            Assert.Equal(Base.AddTicks(expectedTicks), actual);
            Assert.Equal(DateTimeKind.Utc, actual.Kind);
        }

        [Fact]
        public void FractionWithOffset()
        {
            Assert.Equal(new DateTime(2010, 5, 25, 9, 9, 46, 500, DateTimeKind.Utc), ReadText("2010-05-25T11:09:46.5+02:00"));
            Assert.Equal(new DateTime(2010, 5, 25, 13, 9, 46, 50, DateTimeKind.Utc), ReadText("2010-05-25T11:09:46.05-02:00"));
        }

        [Fact]
        public void FractionWithNoZoneUsesTheConfiguredKind()
        {
            DateTime actual = Helper.Read<DateTime>(TextHex("2010-05-25T11:09:46.5"),
                new CborOptions { UnqualifiedTimeZoneDateTimeKind = DateTimeKind.Utc });
            Assert.Equal(new DateTime(2010, 5, 25, 11, 9, 46, 500, DateTimeKind.Utc), actual);
        }

        [Theory]
        [InlineData("2010-05-25T11:09:46.Z")]
        [InlineData("2010-05-25T11:09:46.")]
        [InlineData("2010-05-25T11:09:46.+02:00")]
        [InlineData("2010-05-25T11:09:46.-1Z")]
        [InlineData("2010-05-25T11:09:46.1x")]
        public void MalformedFractionIsRefused(string text)
        {
            CborException exception = null;
            try
            {
                ReadText(text);
            }
            catch (CborException e)
            {
                exception = e;
            }
            Assert.NotNull(exception);
        }

        [Theory]
        [InlineData(500)]
        [InlineData(50)]
        [InlineData(120)]
        [InlineData(5)]
        [InlineData(0)]
        [InlineData(999)]
        public void MillisecondsRoundTrip(int milliseconds)
        {
            var value = new DateTime(2010, 5, 25, 11, 9, 46, milliseconds, DateTimeKind.Utc);
            Assert.Equal(value, Helper.Read<DateTime>(Helper.Write(value)));
        }
    }
}
