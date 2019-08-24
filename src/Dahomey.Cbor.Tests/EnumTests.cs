using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class EnumTests
    {
        [DataTestMethod]
        [DataRow("00", EnumTest.None, null)]
        [DataRow("01", EnumTest.Value1, null)]
        [DataRow("02", EnumTest.Value2, null)]
        [DataRow("03", (EnumTest)3, null)]
        [DataRow("644E6F6E65", EnumTest.None, null)]
        [DataRow("6656616C756531", EnumTest.Value1, null)]
        [DataRow("6656616C756532", EnumTest.Value2, null)]
        [DataRow("67496E76616C6964", (EnumTest)0, typeof(CborException))]
        public void ReadEnum(string hexBuffer, EnumTest expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [DataTestMethod]
        [DataRow("00", EnumTest.None, null, ValueFormat.WriteToInt)]
        [DataRow("01", EnumTest.Value1, null, ValueFormat.WriteToInt)]
        [DataRow("02", EnumTest.Value2, null, ValueFormat.WriteToInt)]
        [DataRow("03", (EnumTest)3, null, ValueFormat.WriteToInt)]
        [DataRow("644E6F6E65", EnumTest.None, null, ValueFormat.WriteToString)]
        [DataRow("6656616C756531", EnumTest.Value1, null, ValueFormat.WriteToString)]
        [DataRow("6656616C756532", EnumTest.Value2, null, ValueFormat.WriteToString)]
        [DataRow("04", (EnumTest)4, null, ValueFormat.WriteToString)]
        public void WriteEnum(string hexBuffer, EnumTest value, Type expectedExceptionType, ValueFormat enumFormat)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType, new CborOptions { EnumFormat = enumFormat });
        }
    }
}
