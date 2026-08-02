using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
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

        private class OutOfOrderObject
        {
            public int Zebra { get; set; }
            public int Apple { get; set; }
            public int Mango { get; set; }
        }

        [Fact]
        public void StringKeyMapMembersAreSortedWhenDeterministic()
        {
            OutOfOrderObject value = new OutOfOrderObject { Zebra = 1, Apple = 2, Mango = 3 };

            // A3 map(3)
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            //   655A65627261 "Zebra"  01
            Helper.TestWrite(value,
                "A3654170706C6502654D616E676F03655A6562726101",
                null,
                new CborOptions { Deterministic = true });
        }

        [Fact]
        public void DeclarationOrderIsPreservedWhenNotDeterministic()
        {
            OutOfOrderObject value = new OutOfOrderObject { Zebra = 1, Apple = 2, Mango = 3 };

            // A3 map(3)
            //   655A65627261 "Zebra"  01
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            Helper.TestWrite(value, "A3655A6562726101654170706C6502654D616E676F03");
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyMapWithNegativeIndex
        {
            [CborProperty(-1)]
            public int Negative { get; set; }
            [CborProperty(0)]
            public int Zero { get; set; }
            [CborProperty(1)]
            public int One { get; set; }
        }

        // ObjectMapping.ValidateMemberNamesAndindexes unconditionally pre-sorts IntKeyMap members by
        // plain ascending int? (ObjectMapping.cs ~line 369), regardless of Deterministic. Ascending
        // int treats -1 as less than 0, so a negative index sorts FIRST here -- this is the pre-existing,
        // non-deterministic behaviour, and it is the control for the next test.
        [Fact]
        public void IntKeyMapNegativeIndexSortsFirstWhenNotDeterministic()
        {
            IntKeyMapWithNegativeIndex value = new IntKeyMapWithNegativeIndex { Negative = 7, Zero = 8, One = 9 };

            // A3 map(3)
            //   20 -1   07
            //   00  0   08
            //   01  1   09
            Helper.TestWrite(value, "A3200700080109");
        }

        // RFC 8949 4.2.1: a negative key is CBOR major type 1, whose leading byte (0x20-0x3B) always
        // exceeds a major type 0 leading byte (0x00-0x1B), so canonical order puts every negative key
        // AFTER every non-negative one -- the opposite of plain ascending int order. CborKeyComparer.
        // CompareIntKeys gets this right; the ascending pre-sort above does not. With Deterministic
        // = true, ObjectConverter's own sort runs after that pre-sort and corrects it: -1 (encoded
        // 0x20) moves from first to last, behind 0 (0x00) and 1 (0x01).
        [Fact]
        public void IntKeyMapNegativeIndexSortsLastWhenDeterministic()
        {
            IntKeyMapWithNegativeIndex value = new IntKeyMapWithNegativeIndex { Negative = 7, Zero = 8, One = 9 };

            // A3 map(3)
            //   00  0   08
            //   01  1   09
            //   20 -1   07
            Helper.TestWrite(value,
                "A3000801092007",
                null,
                new CborOptions { Deterministic = true });
        }

        [CborDiscriminator("Disc")]
        private class DiscriminatedObject
        {
            public int Zebra { get; set; }
            public int Apple { get; set; }
        }

        // DiscriminatorMemberConverter.MemberIndex is hardcoded to 0 in every format (MemberConverter.cs
        // ~line 400), so in a StringKeyMap type carrying a discriminator, the discriminator's
        // IMemberConverter has BOTH a MemberIndex (0) and a MemberName ("_t"). CompareMembersForDeterministicOrder's
        // two-sided `x.MemberIndex.HasValue && y.MemberIndex.HasValue` guard still routes this correctly:
        // ordinary StringKeyMap members have MemberIndex == null, so comparing any of them against the
        // discriminator entry falls through to CompareTextKeys on both MemberNames. "_t" (2 UTF-8 bytes,
        // header 0x62) is shorter-encoded than "Apple"/"Zebra" (5 bytes, header 0x65), and CompareTextKeys
        // orders by encoded length before content, so the discriminator sorts first regardless of its
        // own text, followed by Apple/Zebra alphabetically.
        [Fact]
        public void StringKeyMapWithDiscriminatorIsSortedWhenDeterministic()
        {
            CborOptions options = new CborOptions { Deterministic = true };
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(DiscriminatedObject));
            options.Registry.ObjectMappingRegistry.Register<DiscriminatedObject>(om =>
            {
                om.AutoMap();
                om.SetDiscriminatorPolicy(Dahomey.Cbor.Attributes.CborDiscriminatorPolicy.Always);
            });

            DiscriminatedObject value = new DiscriminatedObject { Zebra = 1, Apple = 2 };

            // A3 map(3)
            //   625F74 "_t"          6444697363 "Disc"
            //   654170706C65 "Apple" 02
            //   655A65627261 "Zebra" 01
            Helper.TestWrite(value,
                "A3625F746444697363654170706C6502655A6562726101",
                null,
                options);
        }

        [Fact]
        public void DictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<string, int> value = new Dictionary<string, int>
            {
                ["b"] = 1,
                ["a"] = 2,
            };

            // A2 map(2) 6161 "a" 02 6162 "b" 01
            Helper.TestWrite(value, "A2616102616201", null, new CborOptions { Deterministic = true });
        }

        [Fact]
        public void DictionaryOrderIsIndependentOfInsertionOrderWhenDeterministic()
        {
            CborOptions options = new CborOptions { Deterministic = true };

            Dictionary<string, int> forwards = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
            Dictionary<string, int> backwards = new Dictionary<string, int> { ["c"] = 3, ["b"] = 2, ["a"] = 1 };

            Assert.Equal(Helper.Write(forwards, options), Helper.Write(backwards, options));
        }

        [Fact]
        public void SerializingTwiceProducesIdenticalBytes()
        {
            CborOptions options = new CborOptions { Deterministic = true };
            OutOfOrderObject value = new OutOfOrderObject { Zebra = 1, Apple = 2, Mango = 3 };

            Assert.Equal(Helper.Write(value, options), Helper.Write(value, options));
        }

        [Fact]
        public void IntKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<int, string> value = new Dictionary<int, string>
            {
                [1] = "one",
                [-1] = "minus one",
                [0] = "zero",
            };

            // A3 map(3)
            //   00  0   64 7A65726F           "zero"
            //   01  1   63 6F6E65              "one"
            //   20 -1   69 6D696E7573206F6E65  "minus one"
            Helper.TestWrite(value,
                "A300647A65726F01636F6E6520696D696E7573206F6E65",
                null,
                new CborOptions { Deterministic = true });
        }

        [Fact]
        public void NonStringNonIntDictionaryKeysThrowWhenDeterministic()
        {
            Dictionary<double, int> value = new Dictionary<double, int>
            {
                [1.5] = 1,
                [2.5] = 2,
            };

            CborOptions options = new CborOptions { Deterministic = true };
            Assert.Throws<CborException>(() => Helper.Write(value, options));
        }
    }
}
