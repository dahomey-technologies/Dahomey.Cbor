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
        [InlineData("781D323031392D30392D31315431303A31363A32382E3834312B30323A3030", "2019-09-11T10:16:28.841+02:00")]
        [InlineData("781D323031392D30392D31315431303A31363A32382E3834312D30323A3030", "2019-09-11T10:16:28.841-02:00")]
        public void ReadDateTime(string hexBuffer, string expectedISO8601)
        {
            DateTime expectedDateTime = DateTime.ParseExact(expectedISO8601,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTime actualDateTime = Helper.Read<DateTime>(hexBuffer);

            Assert.Equal(expectedDateTime, actualDateTime);
        }

        [Theory]
        [InlineData("1A4BFBAFFA", "2010-05-25T11:09:46Z", DateTimeFormat.Unix)]
        [InlineData("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z", DateTimeFormat.ISO8601)]
        [InlineData("7818323031302D30352D32355431313A30393A34362E3132335A", "2010-05-25T11:09:46.123Z", DateTimeFormat.ISO8601)]
        [InlineData("781D323031392D30392D31315431303A31363A32382E3834312B30323A3030", "2019-09-11T10:16:28.841+02:00", DateTimeFormat.ISO8601)]
        [InlineData("781D323031392D30392D31315431343A31363A32382E3834312B30323A3030", "2019-09-11T10:16:28.841-02:00", DateTimeFormat.ISO8601)]
        public void WriteDateTime(string hexBuffer, string value, DateTimeFormat dateTimeFormat)
        {
            DateTime dateTime = DateTime.ParseExact(value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Helper.TestWrite(dateTime, hexBuffer, null, new CborOptions { DateTimeFormat = dateTimeFormat });
        }
    }
}
