using System;
using System.Text;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    // RFC 3339 section 5.6: time-secfrac = "." 1*DIGIT, so ".5" is half a second whatever the number
    // of digits written. A DateTimeOffset reads the fraction exactly as a DateTime does.
    public class DateTimeOffsetFractionTests
    {
        private static string TextHex(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            Assert.InRange(bytes.Length, 0, 255);
            string header = bytes.Length < 24 ? (0x60 + bytes.Length).ToString("X2") : "78" + bytes.Length.ToString("X2");
            return header + Convert.ToHexString(bytes);
        }

        private static DateTimeOffset ReadOffset(string text) => Helper.Read<DateTimeOffset>(TextHex(text));

        private static DateTime ReadDateTime(string text) => Helper.Read<DateTime>(TextHex(text));

        private static readonly DateTimeOffset Base = new DateTimeOffset(2010, 5, 25, 11, 9, 46, TimeSpan.Zero);

        [Theory]
        [InlineData("2010-05-25T11:09:46.5Z", 5000000L)]
        [InlineData("2010-05-25T11:09:46.05Z", 500000L)]
        [InlineData("2010-05-25T11:09:46.50Z", 5000000L)]
        [InlineData("2010-05-25T11:09:46.12Z", 1200000L)]
        [InlineData("2010-05-25T11:09:46.123Z", 1230000L)]
        [InlineData("2010-05-25T11:09:46.123456Z", 1234560L)]
        [InlineData("2010-05-25T11:09:46.1234567Z", 1234567L)]
        [InlineData("2010-05-25T11:09:46.000000001Z", 0L)]       // beyond 100 ns, truncated
        [InlineData("2010-05-25T11:09:46.123456789Z", 1234567L)] // truncated, not rounded
        [InlineData("2010-05-25T11:09:46.999999999Z", 9999999L)] // truncation never carries into the next second
        [InlineData("2010-05-25T11:09:46.0Z", 0L)]
        public void ReadFractionWithZ(string text, long expectedTicks)
        {
            DateTimeOffset actual = ReadOffset(text);

            Assert.Equal(Base.AddTicks(expectedTicks), actual);
            Assert.Equal(Base.Ticks + expectedTicks, actual.Ticks);
            Assert.Equal(TimeSpan.Zero, actual.Offset);
        }

        [Theory]
        [InlineData("2010-05-25T11:09:46.5+02:00", 5000000L, 2, 0)]
        [InlineData("2010-05-25T11:09:46.05-08:00", 500000L, -8, 0)]
        [InlineData("2010-05-25T11:09:46.123456+05:30", 1234560L, 5, 30)]
        [InlineData("2010-05-25T11:09:46.1234567-03:30", 1234567L, -3, -30)]
        [InlineData("2010-05-25T11:09:46.123456789+02:00", 1234567L, 2, 0)]
        public void ReadFractionWithAnOffsetKeepsTheOffsetAndTheTicks(
            string text, long expectedTicks, int offsetHours, int offsetMinutes)
        {
            TimeSpan offset = new TimeSpan(offsetHours, offsetMinutes, 0);

            DateTimeOffset actual = ReadOffset(text);

            Assert.Equal(offset, actual.Offset);
            Assert.Equal(new DateTime(2010, 5, 25, 11, 9, 46).Ticks + expectedTicks, actual.Ticks);
        }

        [Fact]
        public void FractionWithNoOffsetUsesTheConfiguredKind()
        {
            DateTimeOffset actual = Helper.Read<DateTimeOffset>(
                TextHex("2010-05-25T11:09:46.1234567"),
                new CborOptions { UnqualifiedTimeZoneDateTimeKind = DateTimeKind.Utc });

            Assert.Equal(TimeSpan.Zero, actual.Offset);
            Assert.Equal(Base.AddTicks(1234567L), actual);
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
                ReadOffset(text);
            }
            catch (CborException e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
        }

        /// <summary>
        /// The same text names the same instant to both converters. A fraction finer than a millisecond
        /// used to be cut to three digits for a DateTimeOffset and kept to seven for a DateTime.
        /// </summary>
        [Theory]
        [InlineData("2010-05-25T11:09:46.123456789Z")]
        [InlineData("2010-05-25T11:09:46.1234567Z")]
        [InlineData("2010-05-25T11:09:46.123456Z")]
        [InlineData("2010-05-25T11:09:46.12Z")]
        [InlineData("2010-05-25T11:09:46.999999999Z")]
        [InlineData("2010-05-25T11:09:46.05+02:00")]
        [InlineData("2010-05-25T11:09:46.123456789-08:00")]
        public void BothConvertersAgreeOnTheInstant(string text)
        {
            Assert.Equal(ReadDateTime(text).Ticks, ReadOffset(text).UtcTicks);
        }

        [Fact]
        public void ANineDigitFractionIsNoLongerCutToMilliseconds()
        {
            Assert.Equal(1234567L, ReadOffset("2010-05-25T11:09:46.123456789Z").Ticks - Base.Ticks);
        }

        /// <summary>
        /// A millisecond-resolution value survives a write and a read, including its offset.
        /// </summary>
        [Theory]
        [InlineData(500, 2)]
        [InlineData(50, -8)]
        [InlineData(5, 0)]
        [InlineData(999, 5)]
        [InlineData(0, -3)]
        public void MillisecondsRoundTrip(int milliseconds, int offsetHours)
        {
            var value = new DateTimeOffset(2010, 5, 25, 11, 9, 46, milliseconds, new TimeSpan(offsetHours, 0, 0));

            DateTimeOffset read = Helper.Read<DateTimeOffset>(Helper.Write(value));

            Assert.Equal(value, read);
            Assert.Equal(value.Offset, read.Offset);
        }

        /// <summary>
        /// The write path emits <c>FFF</c>: milliseconds, truncated. A tick finer than that is read
        /// exactly but is not written, so a value carrying one comes back cut to the millisecond.
        /// That is the write side's resolution and is left as it is.
        /// </summary>
        [Fact]
        public void TheWriteKeepsMillisecondsOnly()
        {
            var value = new DateTimeOffset(2010, 5, 25, 11, 9, 46, new TimeSpan(2, 0, 0)).AddTicks(1234567);

            DateTimeOffset read = Helper.Read<DateTimeOffset>(Helper.Write(value));

            Assert.Equal(value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMillisecond)), read);
            Assert.Equal(value.Offset, read.Offset);
        }
    }
}
