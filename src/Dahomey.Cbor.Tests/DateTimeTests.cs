using Xunit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Tests
{

    public class DateTimeTests
    {
        [Theory]
        [InlineData("1A4BFBAFFA", "2010-05-25T11:09:46Z")]
        [InlineData("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z")]
        public void ReadDateTime(string hexBuffer, string expectedISO8601)
        {
            DateTime actualDateTime = Helper.Read<DateTime>(hexBuffer);
            DateTime expectedDateTime = DateTime.ParseExact(expectedISO8601,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.Equal(expectedDateTime, actualDateTime);
        }

        [Theory]
        [InlineData("1A4BFBAFFA", "2010-05-25T11:09:46Z", DateTimeFormat.Unix)]
        [InlineData("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z", DateTimeFormat.ISO8601)]
        public void WriteDateTime(string hexBuffer, string value, DateTimeFormat dateTimeFormat)
        {
            DateTime dateTime = DateTime.ParseExact(value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Helper.TestWrite(dateTime, hexBuffer, null, new CborOptions { DateTimeFormat = dateTimeFormat });
        }
    }
}
