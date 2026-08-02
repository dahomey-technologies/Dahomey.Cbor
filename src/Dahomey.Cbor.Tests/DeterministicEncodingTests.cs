using System;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class DeterministicEncodingTests
    {
        [Fact]
        public void DeterministicIsOffByDefault()
        {
            Assert.False(new CborOptions().Deterministic);
        }

        [Fact]
        public void DeterministicRejectsIndefiniteArrayLength()
        {
            CborOptions options = new CborOptions { ArrayLengthMode = LengthMode.IndefiniteLength };
            Assert.Throws<CborException>(() => options.Deterministic = true);
        }

        [Fact]
        public void DeterministicRejectsIndefiniteMapLength()
        {
            CborOptions options = new CborOptions { MapLengthMode = LengthMode.IndefiniteLength };
            Assert.Throws<CborException>(() => options.Deterministic = true);
        }

        [Fact]
        public void IndefiniteLengthIsRejectedOnceDeterministic()
        {
            CborOptions options = new CborOptions { Deterministic = true };
            Assert.Throws<CborException>(() => options.ArrayLengthMode = LengthMode.IndefiniteLength);
            Assert.Throws<CborException>(() => options.MapLengthMode = LengthMode.IndefiniteLength);
        }

        [Fact]
        public void DefiniteLengthIsStillAllowedWhenDeterministic()
        {
            CborOptions options = new CborOptions { Deterministic = true };
            options.ArrayLengthMode = LengthMode.DefiniteLength;
            options.MapLengthMode = LengthMode.DefiniteLength;
            Assert.True(options.Deterministic);
        }

        private static int CompareNames(string a, string b)
        {
            return Math.Sign(Dahomey.Cbor.Serialization.CborKeyComparer.CompareTextKeys(
                System.Text.Encoding.UTF8.GetBytes(a),
                System.Text.Encoding.UTF8.GetBytes(b)));
        }

        [Theory]
        [InlineData("a", "b", -1)]     // same length, bytewise
        [InlineData("b", "a", 1)]
        [InlineData("a", "a", 0)]
        [InlineData("z", "aa", -1)]    // shorter encoded key sorts first
        [InlineData("aa", "z", 1)]
        [InlineData("", "a", -1)]      // empty key is the smallest
        public void TextKeysSortBytewiseOnTheEncodedForm(string a, string b, int expected)
        {
            Assert.Equal(expected, CompareNames(a, b));
        }

        [Theory]
        [InlineData(0, 1, -1)]
        [InlineData(23, 24, -1)]       // 0x17 then 0x18 0x18 -- still ascending
        [InlineData(255, 256, -1)]     // 0x18 FF then 0x19 01 00 -- still ascending
        [InlineData(1, 1, 0)]
        [InlineData(0, -1, -1)]        // negative keys are major type 1, so they sort last
        [InlineData(-1, 0, 1)]
        [InlineData(-1, -2, -1)]
        public void IntKeysSortBytewiseOnTheEncodedForm(int a, int b, int expected)
        {
            Assert.Equal(expected, Math.Sign(Dahomey.Cbor.Serialization.CborKeyComparer.CompareIntKeys(a, b)));
        }
    }
}
