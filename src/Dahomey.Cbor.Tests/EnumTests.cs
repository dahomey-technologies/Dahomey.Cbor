using Xunit;
using System;

namespace Dahomey.Cbor.Tests
{

    public class EnumTests
    {
        [Theory]
        [InlineData("00", EnumTest.None, null)]
        [InlineData("01", EnumTest.Value1, null)]
        [InlineData("02", EnumTest.Value2, null)]
        [InlineData("03", (EnumTest)3, null)]
        [InlineData("644E6F6E65", EnumTest.None, null)]
        [InlineData("6656616C756531", EnumTest.Value1, null)]
        [InlineData("6656616C756532", EnumTest.Value2, null)]
        [InlineData("67496E76616C6964", (EnumTest)0, typeof(CborException))]
        public void ReadEnum(string hexBuffer, EnumTest expectedValue, Type expectedExceptionType)
        {
            Helper.TestRead(hexBuffer, expectedValue, expectedExceptionType);
        }

        [Theory]
        [InlineData("00", EnumTest.None, null, ValueFormat.WriteToInt)]
        [InlineData("01", EnumTest.Value1, null, ValueFormat.WriteToInt)]
        [InlineData("02", EnumTest.Value2, null, ValueFormat.WriteToInt)]
        [InlineData("03", (EnumTest)3, null, ValueFormat.WriteToInt)]
        [InlineData("644E6F6E65", EnumTest.None, null, ValueFormat.WriteToString)]
        [InlineData("6656616C756531", EnumTest.Value1, null, ValueFormat.WriteToString)]
        [InlineData("6656616C756532", EnumTest.Value2, null, ValueFormat.WriteToString)]
        [InlineData("04", (EnumTest)4, null, ValueFormat.WriteToString)]
        public void WriteEnum(string hexBuffer, EnumTest value, Type expectedExceptionType, ValueFormat enumFormat)
        {
            Helper.TestWrite(value, hexBuffer, expectedExceptionType, new CborOptions { EnumFormat = enumFormat });
        }
    }
}
