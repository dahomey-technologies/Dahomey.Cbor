using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class DateTimeTests
    {
        [DataTestMethod]
        [DataRow("1A4BFBAFFA", "2010-05-25T11:09:46Z")]
        [DataRow("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z")]
        public void ReadDateTime(string hexBuffer, string expectedISO8601)
        {
            DateTime actualDateTime = Helper.Read<DateTime>(hexBuffer);
            DateTime expectedDateTime = DateTime.ParseExact(expectedISO8601,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.AreEqual(expectedDateTime, actualDateTime);
        }

        [DataTestMethod]
        [DataRow("1A4BFBAFFA", "2010-05-25T11:09:46Z", DateTimeFormat.Unix)]
        [DataRow("74323031302D30352D32355431313A30393A34365A", "2010-05-25T11:09:46Z", DateTimeFormat.ISO8601)]
        public void WriteDateTime(string hexBuffer, string value, DateTimeFormat dateTimeFormat)
        {
            DateTime dateTime = DateTime.ParseExact(value,
                "yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Helper.TestWrite(dateTime, hexBuffer, null, new CborOptions { DateTimeFormat = dateTimeFormat });
        }
    }
}
