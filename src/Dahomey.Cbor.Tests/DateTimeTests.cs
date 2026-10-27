using Xunit;
using System;
using System.Globalization;
using Dahomey.Cbor.Util;
using Dahomey.Cbor.Serialization;

namespace Dahomey.Cbor.Tests
{
    public class DateTimeTests
    {
        [Theory]
        [InlineData("1A4BFBAFFA", "2010-05-25T11:09:46Z")]
        [InlineData("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z")]
        [InlineData("7818323031302D30352D32355431313A30393A34362E3132335A", "2010-05-25T11:09:46.123Z")]
        // An offset is subtracted to reach UTC, so "+02:00" reads two hours earlier and "-02:00" two
        // hours later -- not the reverse.
        [InlineData("781D323031392D30392D31315431303A31363A32382E3834312B30323A3030", "2019-09-11T08:16:28.841Z")]
        [InlineData("781D323031392D30392D31315431303A31363A32382E3834312D30323A3030", "2019-09-11T12:16:28.841Z")]
        // RFC 3339 section 5.6's own worked example, which states this equivalence outright, and whose
        // offset also carries the date across midnight.
        [InlineData("7819313939362D31322D31395431363A33393A35372D30383A3030", "1996-12-20T00:39:57Z")]
        // A minutes-bearing offset, so the two components are known to be applied in the same direction.
        [InlineData("7819323031392D30392D31315431303A31363A32382B30353A3330", "2019-09-11T04:46:28Z")]
        public void ReadDateTime(string hexBuffer, string expectedISO8601)
        {
            DateTime expectedDateTime = DateTime.ParseExact(expectedISO8601,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTime actualDateTime = Helper.Read<DateTime>(hexBuffer);

            Assert.Equal(expectedDateTime, actualDateTime);
        }

        [Theory]
        [InlineData("C11A4BFBAFFA", "2010-05-25T11:09:46Z", DateTimeFormat.Unix)]
        [InlineData("C1FB41D2FEEBFE87DF3B", "2010-05-25T11:09:46.123Z", DateTimeFormat.UnixMilliseconds)]
        [InlineData("C074323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z", DateTimeFormat.ISO8601)]
        [InlineData("C07818323031302D30352D32355431313A30393A34362E3132335A", "2010-05-25T11:09:46.123Z", DateTimeFormat.ISO8601)]
        //[InlineData("781D323031392D30392D31315431303A31363A32382E3834312B30323A3030", "2019-09-11T10:16:28.841+02:00", DateTimeFormat.ISO8601)]
        //[InlineData("781D323031392D30392D31315431343A31363A32382E3834312B30323A3030", "2019-09-11T10:16:28.841-02:00", DateTimeFormat.ISO8601)]
        public void WriteDateTime(string hexBuffer, string value, DateTimeFormat dateTimeFormat)
        {
            DateTime dateTime = DateTime.ParseExact(value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Helper.TestWrite(dateTime, hexBuffer, null, new CborOptions { DateTimeFormat = dateTimeFormat });
        }

        [Theory]
        [InlineData("C11A64633F08")] // Unsigned with tag
        [InlineData("1A64633F08")] // Unsigned without tag
        public void ReadUnixTimestamp(string hexBuffer)
        {
            var dateTime = Helper.Read<DateTime>(hexBuffer);

            Assert.Equal(new DateTime(2023, 05, 16, 08, 30, 00, DateTimeKind.Utc), dateTime);
            Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
        }

        [Theory]
        [InlineData("C1F97E00")]              // NaN, half
        [InlineData("C1FA7FC00000")]          // NaN, single
        [InlineData("C1FB7FF8000000000000")]  // NaN, double
        [InlineData("C1FB7FF0000000000000")]  // +Infinity, double
        [InlineData("C1FBFFF0000000000000")]  // -Infinity, double
        public void ANonFiniteTimeStampIsRefused(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<DateTime>(hexBuffer));
        }

        /// <summary>
        /// The double written for a millisecond is only the nearest one to it, a tick or two either
        /// side, so reading it back must land on that millisecond rather than truncate.
        /// </summary>
        [Fact]
        public void UnixMillisecondsRoundTripsEveryMillisecond()
        {
            CborOptions options = new CborOptions { DateTimeFormat = DateTimeFormat.UnixMilliseconds };

            foreach (DateTime start in new[]
            {
                new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                new DateTime(2262, 4, 11, 23, 47, 16, DateTimeKind.Utc),
                new DateTime(1969, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            })
            {
                for (int i = 0; i < 2000; i++)
                {
                    DateTime value = start.AddMilliseconds(i);
                    Assert.Equal(value, Helper.Read<DateTime>(Helper.Write(value, options), options));
                }
            }
        }

        [Theory]
        [InlineData("C1FB41D2FEEBFE87DF3B", "2010-05-25T11:09:46.123Z")]
        [InlineData("C1FB3FF8000000000000", "1970-01-01T00:00:01.5Z")]
        // Not a whole millisecond, so read to the nearest tick: 1.00000012 s is 10000001.2 ticks.
        [InlineData("C1FB3FF0000020365653", "1970-01-01T00:00:01.0000001Z")]
        public void ReadFractionalUnixTime(string hexBuffer, string expectedISO8601)
        {
            DateTime expected = DateTime.Parse(expectedISO8601, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            Assert.Equal(expected, Helper.Read<DateTime>(hexBuffer));
        }
    }
}
