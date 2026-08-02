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
        [InlineData("a", "b", -1)]     // same length (1 vs 1), same header size tier: falls through to content bytewise
        [InlineData("b", "a", 1)]
        [InlineData("a", "a", 0)]
        [InlineData("z", "aa", -1)]    // length 1 vs 2, both single-byte headers (same size TIER): header length VALUE decides, content never compared
        [InlineData("aa", "z", 1)]
        [InlineData("", "a", -1)]      // empty key is the smallest
        public void TextKeysSortBytewiseOnTheEncodedForm(string a, string b, int expected)
        {
            Assert.Equal(expected, CompareNames(a, b));
        }

        // These cross an actual header-size TIER boundary (1-byte header <-> 2-byte header <-> 3-byte
        // header), not just a length value within one tier. The content is deliberately chosen so that
        // raw content comparison disagrees with the correct answer: a naive CompareTextKeys that skipped
        // the header-size step and just called SequenceCompareTo on the raw name bytes would get these
        // backwards, which is exactly the bug this test exists to catch.
        //
        // len 23 'z' (0x7A) encodes as header 0x77 + 23 content bytes = 24 bytes total: [77 7A*23].
        // len 24 'a' (0x61) encodes as header 78 18 + 24 content bytes = 26 bytes total: [78 18 61*24].
        // First byte 0x77 < 0x78, so the 23-char key sorts first even though 'z' > 'a' by content.
        //
        // len 255 'z' encodes as header 78 FF + 255 content bytes = 257 bytes: [78 FF 7A*255].
        // len 256 'a' encodes as header 79 01 00 + 256 content bytes = 259 bytes: [79 01 00 61*256].
        // First byte 0x78 < 0x79, so the 255-char key sorts first even though 'z' > 'a' by content.
        //
        // len 65535 'z' encodes as header 79 FF FF (additional-info 25: a 2-byte length field, and
        // 65535 == 0xFFFF fits exactly) + 65535 content bytes = 65538 bytes: [79 FF FF 7A*65535].
        // len 65536 'a' exceeds the 2-byte length field's range, so it needs additional-info 26 (a
        // 4-byte length field): header 7A 00 01 00 00 (65536 == 0x00010000) + 65536 content bytes =
        // 65541 bytes: [7A 00 01 00 00 61*65536].
        // First byte 0x79 < 0x7A, so the 65535-char key sorts first even though 'z' > 'a' by content.
        // This is CompareTextKeys's only 2-byte-length-field <-> 4-byte-length-field crossing; unlike
        // the int side, CompareTextKeys has no non-negative shortcut, so this tier boundary is always
        // on the comparison path and was previously unexercised.
        [Theory]
        [InlineData(23, 'z', 24, 'a', -1)]
        [InlineData(24, 'a', 23, 'z', 1)]
        [InlineData(255, 'z', 256, 'a', -1)]
        [InlineData(256, 'a', 255, 'z', 1)]
        [InlineData(65535, 'z', 65536, 'a', -1)]
        [InlineData(65536, 'a', 65535, 'z', 1)]
        public void TextKeysCrossHeaderSizeTiersEvenWhenContentDisagrees(int lengthA, char charA, int lengthB, char charB, int expected)
        {
            string a = new string(charA, lengthA);
            string b = new string(charB, lengthB);
            Assert.Equal(expected, CompareNames(a, b));
        }

        [Theory]
        [InlineData(0, 1, -1)]
        [InlineData(23, 24, -1)]       // 0x17 then 0x18 0x18 -- still ascending
        [InlineData(255, 256, -1)]     // 0x18 FF then 0x19 01 00 -- still ascending
        [InlineData(1, 1, 0)]
        [InlineData(0, -1, -1)]        // negative keys are major type 1, so they sort last
        [InlineData(-1, 0, 1)]
        [InlineData(-1, -2, -1)]       // -1 = 0x20, -2 = 0x21: ascending bytewise, so -1 sorts before -2
        public void IntKeysSortBytewiseOnTheEncodedForm(int a, int b, int expected)
        {
            Assert.Equal(expected, Math.Sign(Dahomey.Cbor.Serialization.CborKeyComparer.CompareIntKeys(a, b)));
        }

        // Cross an argument-size TIER boundary (1-byte arg <-> 2-byte arg <-> 4-byte arg -- CBOR has no
        // 3-byte argument form, so the 65535/65536 pair jumps two tiers at once). The non-negative pair
        // exercises the same shortcut as the table above; the negative pairs are the only cases that
        // reach CborKeyComparer's private CompareArgumentEncoding ladder at all, since CompareIntKeys
        // only calls it from the a<0 && b<0 branch.
        //
        // -24  -> argument 23  -> 0x37 (1 byte).
        // -25  -> argument 24  -> 0x38 0x18 (2 bytes). 0x37 < 0x38, so -24 sorts before -25.
        //
        // -256  -> argument 255  -> 0x38 0xFF (2 bytes).
        // -257  -> argument 256  -> 0x39 0x01 0x00 (3 bytes). 0x38 < 0x39, so -256 sorts before -257.
        //
        // -65536  -> argument 65535 -> 0x39 0xFF 0xFF (3 bytes).
        // -65537  -> argument 65536 -> 0x3A 0x00 0x01 0x00 0x00 (5 bytes). 0x39 < 0x3A, so -65536 sorts
        // before -65537.
        [Theory]
        [InlineData(65535, 65536, -1)]
        [InlineData(65536, 65535, 1)]
        [InlineData(-24, -25, -1)]
        [InlineData(-25, -24, 1)]
        [InlineData(-256, -257, -1)]
        [InlineData(-257, -256, 1)]
        [InlineData(-65536, -65537, -1)]
        [InlineData(-65537, -65536, 1)]
        public void IntKeysCrossArgumentSizeTiers(int a, int b, int expected)
        {
            Assert.Equal(expected, Math.Sign(Dahomey.Cbor.Serialization.CborKeyComparer.CompareIntKeys(a, b)));
        }

        // int.MinValue is the brief's named overflow trap: -1 - int.MinValue overflows a 32-bit
        // computation (int.MinValue has no positive counterpart in Int32). CompareIntKeys computes
        // -1L - a in `long`, where int.MinValue promotes to -2147483648L and -1L - (-2147483648L) =
        // 2147483647L, which fits comfortably in both long and the target ulong. These cases pin that:
        // if the subtraction were ever narrowed back to `int`, this is the value that would misbehave.
        //
        // int.MinValue -> argument 2147483647 (5-byte form, header 0x3A) vs -1 -> argument 0 (1-byte
        // form, header 0x20): 0x20 < 0x3A, so -1 sorts before int.MinValue.
        [Theory]
        [InlineData(int.MinValue, -1, 1)]
        [InlineData(-1, int.MinValue, -1)]
        [InlineData(int.MinValue, 0, 1)]
        [InlineData(0, int.MinValue, -1)]
        public void IntKeysHandleMinValueWithoutOverflow(int a, int b, int expected)
        {
            Assert.Equal(expected, Math.Sign(Dahomey.Cbor.Serialization.CborKeyComparer.CompareIntKeys(a, b)));
        }
    }
}
