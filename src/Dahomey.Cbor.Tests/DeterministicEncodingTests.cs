using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
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

        // Every integral CLR type is a working dictionary key without Deterministic, so turning the flag
        // on must not take any of them away: an opt-in guarantee that breaks working code is a poor
        // trade. Each is decorated as the major type its own key converter emits, so the order computed
        // is the order of the bytes actually written -- which is why these expectations are the
        // converters' own encodings, not a normalisation of them.
        [Fact]
        public void ByteKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<byte, int> value = new Dictionary<byte, int>
            {
                [200] = 1,
                [1] = 2,
            };

            // A2 map(2)
            //   01     1    02
            //   18C8 200    01
            Helper.TestWrite(value, "A2010218C801", null, new CborOptions { Deterministic = true });
        }

        [Fact]
        public void SByteKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<sbyte, int> value = new Dictionary<sbyte, int>
            {
                [-1] = 1,
                [2] = 2,
            };

            // A2 map(2)
            //   02   2   02
            //   20  -1   01      -- major type 1 sorts after every major type 0 key
            Helper.TestWrite(value, "A202022001", null, new CborOptions { Deterministic = true });
        }

        [Fact]
        public void ShortKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<short, int> value = new Dictionary<short, int>
            {
                [-2] = 1,
                [3] = 2,
            };

            // A2 map(2)
            //   03   3   02
            //   21  -2   01
            Helper.TestWrite(value, "A203022101", null, new CborOptions { Deterministic = true });
        }

        [Fact]
        public void UShortKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<ushort, int> value = new Dictionary<ushort, int>
            {
                [65535] = 1,
                [7] = 2,
            };

            // A2 map(2)
            //   07          7   02
            //   19FFFF  65535   01
            Helper.TestWrite(value, "A2070219FFFF01", null, new CborOptions { Deterministic = true });
        }

        [Fact]
        public void UIntKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<uint, string> value = new Dictionary<uint, string>
            {
                [4294967295] = "big",
                [1] = "small",
            };

            // A2 map(2)
            //   01                    1   65736D616C6C  "small"
            //   1AFFFFFFFF   4294967295   63626967      "big"
            Helper.TestWrite(value,
                "A20165736D616C6C1AFFFFFFFF63626967",
                null,
                new CborOptions { Deterministic = true });
        }

        private enum Signal
        {
            Stop = -1,
            Go = 0,
            Wait = 1,
        }

        [Fact]
        public void EnumKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<Signal, int> value = new Dictionary<Signal, int>
            {
                [Signal.Stop] = 1,
                [Signal.Go] = 2,
                [Signal.Wait] = 3,
            };

            // With the default EnumFormat (WriteToInt) the keys are plain integers, so Stop (-1) is
            // major type 1 and sorts last.
            //
            // A3 map(3)
            //   00   Go    0   02
            //   01   Wait  1   03
            //   20   Stop -1   01
            Helper.TestWrite(value, "A3000201032001", null, new CborOptions { Deterministic = true });
        }

        // A boxed enum is not an `int` -- unboxing is exact-type, so `key is int` is false for
        // Signal.Go however int-like its underlying type is. That is what made enum keys throw, and it
        // is why enums get a branch of their own rather than falling into the integral one.
        [Fact]
        public void EnumKeyedDictionaryFollowsEnumFormatWhenDeterministic()
        {
            Dictionary<Signal, int> value = new Dictionary<Signal, int>
            {
                [Signal.Wait] = 1,
                [Signal.Go] = 2,
            };

            CborOptions options = new CborOptions
            {
                Deterministic = true,
                EnumFormat = ValueFormat.WriteToString,
            };

            // WriteToString writes each key as its member name, so these are text keys and are ordered
            // as text: "Go" encodes in 3 bytes, "Wait" in 5, and shorter encodings sort first.
            //
            // A2 map(2)
            //   62476F      "Go"    02
            //   6457616974  "Wait"  01
            Helper.TestWrite(value, "A262476F02645761697401", null, options);
        }

        [Fact]
        public void ByteArrayKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<byte[], int> value = new Dictionary<byte[], int>
            {
                [new byte[] { 1, 1 }] = 1,
                [new byte[] { 2 }] = 2,
            };

            // Byte-string keys (major type 2) order exactly like text keys: by encoded length first,
            // then bytewise on the content. h'02' encodes in 2 bytes, h'0101' in 3.
            //
            // A2 map(2)
            //   4102    h'02'    02
            //   420101  h'0101'  01
            Helper.TestWrite(value, "A241020242010101", null, new CborOptions { Deterministic = true });
        }

        /// <summary>
        /// A key type with no special case anywhere in the ordering code still sorts correctly,
        /// because the order comes from the bytes its own converter writes.
        /// </summary>
        [Fact]
        public void FloatingPointDictionaryKeysAreSortedByTheirEncodedBytes()
        {
            Dictionary<double, int> value = new Dictionary<double, int>
            {
                [2.5] = 2,
                [1.5] = 1,
            };

            // Both are exactly representable as half floats, so preferred float serialization writes
            // each in three bytes and the comparison is on those.
            //
            // A2 map(2)
            //   F93E00  1.5  01
            //   F94100  2.5  02
            Helper.TestWrite(value, "A2F93E0001F9410002", null, new CborOptions { Deterministic = true });
        }

        [Fact]
        public void CborObjectStringKeysAreSortedWhenDeterministic()
        {
            CborObject value = new CborObject
            {
                ["zebra"] = 1,
                ["apple"] = 2,
            };

            // A2 map(2)
            //   656170706C65 "apple" 02
            //   657A65627261 "zebra" 01
            Helper.TestWrite(value,
                "A2656170706C6502657A6562726101",
                null,
                new CborOptions { Deterministic = true });

            // Insertion order is zebra, apple, so the non-deterministic encoding disagrees with the
            // sorted one above -- pinning that this is actually the sort taking effect, not a
            // coincidence of Dictionary's default enumeration order.
            Assert.NotEqual(Helper.Write(value), Helper.Write(value, new CborOptions { Deterministic = true }));
        }

        [Fact]
        public void CborObjectIntegerKeysSortMajorTypeZeroBeforeMajorTypeOneWhenDeterministic()
        {
            CborObject value = new CborObject
            {
                [1] = "one",
                [-1] = "minus one",
                [0] = "zero",
            };

            // Same rule as IntKeyedDictionaryKeysAreSortedWhenDeterministic above: CborPositive (major
            // type 0) always sorts before CborNegative (major type 1), so 0 and 1 both precede -1
            // despite -1 > ... being false in plain numeric order.
            //
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
        public void CborObjectByteStringKeysAreSortedWhenDeterministic()
        {
            CborObject value = new CborObject
            {
                [new ReadOnlyMemory<byte>(new byte[] { 1, 1 })] = 1,
                [new ReadOnlyMemory<byte>(new byte[] { 2 })] = 2,
            };

            // Byte strings are legal CBOR map keys (major type 2) and are ordered by the same rule as
            // text: encoded length first, then bytewise on the content.
            //
            // A2 map(2)
            //   4102    h'02'    02
            //   420101  h'0101'  01
            Helper.TestWrite(value, "A241020242010101", null, new CborOptions { Deterministic = true });
        }

        // A map whose keys are all one kind is the easy case; a map read off the wire is under no
        // obligation to be one. RFC 8949 4.2.1 orders mixed kinds by major type first, which is just the
        // leading byte's own order: 0 unsigned, then 1 negative, then 2 byte string, then 3 text.
        [Fact]
        public void CborObjectMixedKeyKindsSortByMajorTypeWhenDeterministic()
        {
            CborObject value = new CborObject
            {
                ["text"] = 1,
                [new ReadOnlyMemory<byte>(new byte[] { 0xFF })] = 2,
                [-1] = 3,
                [7] = 4,
            };

            // A4 map(4)
            //   07            7        04
            //   20           -1        03
            //   41FF         h'FF'     02
            //   6474657874   "text"    01
            Helper.TestWrite(value,
                "A40704200341FF02647465787401",
                null,
                new CborOptions { Deterministic = true });
        }

        // The headline use case: a document arrives, is decoded, and has to be re-encoded to be hashed.
        // Its keys are whatever the sender chose, in whatever order the sender wrote them, so the
        // deterministic path has to accept every key kind the reader can produce -- you cannot hash what
        // you cannot re-encode.
        [Fact]
        public void CborObjectReadFromTheWireReEncodesDeterministically()
        {
            // A4 map(4), keys deliberately out of canonical order on the wire:
            //   6474657874   "text"    01
            //   41FF         h'FF'     02
            //   20           -1        03
            //   07            7        04
            const string scrambled = "A464746578740141FF0220030704";

            CborObject value = Cbor.Deserialize<CborObject>(scrambled.HexToBytes());

            Helper.TestWrite(value,
                "A40704200341FF02647465787401",
                null,
                new CborOptions { Deterministic = true });
        }

        /// <summary>
        /// A CborObject may mix key kinds, and a boolean key is neither a string nor an integer.
        /// Ordering by the encoded bytes covers it without needing to know what a boolean is.
        /// </summary>
        [Fact]
        public void CborObjectMixedKeyKindsAreSortedByTheirEncodedBytes()
        {
            CborObject value = new CborObject
            {
                [true] = 1,
                ["ok"] = 2,
            };

            // A2 map(2)
            //   626F6B  "ok"  02
            //   F5      true  01
            Helper.TestWrite(value, "A2626F6B02F501", null, new CborOptions { Deterministic = true });
        }

        // Reviewer's exact reproduction of the int-narrowing bug: a key that needs the 9-byte
        // (major-type + 8-byte-argument) form must still sort AFTER a key that fits in 1 byte, per RFC
        // 8949 4.2.1's "shorter encoding always sorts first" rule -- regardless of numeric magnitude.
        // Before CborKeyComparer.CompareIntegerKeys, CborValueConverter read both keys through
        // Value<int>(), which silently wrapped 4294967301 (2^32 + 5) down to 5, comparing it as if it
        // were smaller than 10 and emitting the 9-byte key first: wrong order, wrong bytes, no
        // exception. TryGetIntegerKeyArgument now reads the full ulong via Value<ulong>(), so the
        // 9-byte form correctly sorts last.
        [Fact]
        public void CborObjectLargeIntegerKeysSortByEncodedLengthNotNumericValueWhenDeterministic()
        {
            CborObject value = new CborObject
            {
                [(ulong)4294967301] = "A", // 2^32 + 5: needs the 9-byte (1B + 8-byte) argument form
                [(ulong)10] = "B",         // fits the 1-byte argument form
            };

            // A2 map(2)
            //   0A                   10                  6142 "B"
            //   1B0000000100000005   4294967301          6141 "A"
            Helper.TestWrite(value,
                "A20A61421B00000001000000056141",
                null,
                new CborOptions { Deterministic = true });
        }

        [Fact]
        public void CborObjectUlongMaxValueKeySortsAfterSmallKeyWhenDeterministic()
        {
            CborObject value = new CborObject
            {
                [ulong.MaxValue] = "big",
                [(ulong)1] = "small",
            };

            // A2 map(2)
            //   01                    1                      65736D616C6C  "small"
            //   1BFFFFFFFFFFFFFFFF    18446744073709551615   63626967      "big"
            Helper.TestWrite(value,
                "A20165736D616C6C1BFFFFFFFFFFFFFFFF63626967",
                null,
                new CborOptions { Deterministic = true });
        }

        // A negative key beyond long.MinValue (down to CBOR's true floor of -2^64) is not exercised
        // here: CborNegative's constructor takes a `long` and rejects anything a `long` cannot hold,
        // and the reader path (CborReader.ReadInt64 -> ReadSigned(long.MaxValue)) is bounded the same
        // way, so no CborValue in this object model can ever hold a value below long.MinValue. There is
        // no case to construct. CborKeyComparer.CompareIntegerKeys still accepts the full ulong argument
        // range on its own terms -- verified directly by the CborKeyComparer.CompareIntegerKeys unit
        // tests below -- it is only CborValue's own representation that stops at long.MinValue.

        [Theory]
        [InlineData(false, 10ul, false, 4294967301ul, -1)]     // both major type 0: shorter-encoded argument (10, 1 byte) sorts first
        [InlineData(false, 4294967301ul, false, 10ul, 1)]
        [InlineData(false, 0ul, true, 0ul, -1)]                // major type 0 (any argument) sorts before major type 1 (any argument)
        [InlineData(true, 0ul, false, 0ul, 1)]
        [InlineData(true, 0ul, true, ulong.MaxValue, -1)]      // within major type 1, ascending argument is ascending encoded order
        [InlineData(true, ulong.MaxValue, true, 0ul, 1)]
        [InlineData(false, ulong.MaxValue, false, ulong.MaxValue, 0)]
        public void CompareIntegerKeysOrdersByMajorTypeThenEncodedArgument(
            bool negativeA, ulong argumentA, bool negativeB, ulong argumentB, int expected)
        {
            Assert.Equal(expected, Math.Sign(
                Dahomey.Cbor.Serialization.CborKeyComparer.CompareIntegerKeys(negativeA, argumentA, negativeB, argumentB)));
        }

        // CompareKeys is the one comparison that spans kinds: it takes a major type plus whatever varies
        // within that type (the argument for integers, the payload for strings). Major types are ordered
        // by their own numeric order, which is the order of the encoded leading byte -- 0 unsigned, 1
        // negative, 2 byte string, 3 text string -- so the comparison never has to look at the payload of
        // two keys of different kinds, however those payloads would compare.
        [Fact]
        public void CompareKeysOrdersByMajorTypeBeforeAnythingElse()
        {
            ReadOnlySpan<byte> noContent = default;
            ReadOnlySpan<byte> content = new byte[] { 0x41 };

            // Largest possible unsigned key still sorts before the smallest possible negative key.
            Assert.True(Serialization.CborKeyComparer.CompareKeys(
                Serialization.CborMajorType.PositiveInteger, ulong.MaxValue, noContent,
                Serialization.CborMajorType.NegativeInteger, 0, noContent) < 0);

            // Negative before byte string.
            Assert.True(Serialization.CborKeyComparer.CompareKeys(
                Serialization.CborMajorType.NegativeInteger, ulong.MaxValue, noContent,
                Serialization.CborMajorType.ByteString, 0, content) < 0);

            // Byte string before text string, with identical payloads on both sides -- only the major
            // type can be deciding this.
            Assert.True(Serialization.CborKeyComparer.CompareKeys(
                Serialization.CborMajorType.ByteString, 0, content,
                Serialization.CborMajorType.TextString, 0, content) < 0);

            // Within one major type the existing rules apply unchanged.
            Assert.Equal(0, Serialization.CborKeyComparer.CompareKeys(
                Serialization.CborMajorType.TextString, 0, content,
                Serialization.CborMajorType.TextString, 0, content));
        }

        [Fact]
        public void CompareKeysRejectsMajorTypesThatAreNotKeys()
        {
            // Nothing decorates a key as an array, so this is unreachable from the converters; it is
            // pinned here so the rejection stays a CborException rather than, say, a silent 0 that would
            // make the sort claim two different keys are equal.
            Assert.Throws<CborException>(() => Serialization.CborKeyComparer.CompareKeys(
                Serialization.CborMajorType.Array, 0, default,
                Serialization.CborMajorType.Array, 0, default));
        }

        // OPTIONAL widening that fell out of CompareIntegerKeys existing: long/ulong dictionary keys no
        // longer have to throw, since the comparer they need already exists for CborObject keys.
        [Fact]
        public void LongKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<long, string> value = new Dictionary<long, string>
            {
                [4294967301L] = "A",
                [10L] = "B",
            };

            // Same wire bytes as CborObjectLargeIntegerKeysSortByEncodedLengthNotNumericValueWhenDeterministic --
            // see that test for the byte-by-byte derivation.
            Helper.TestWrite(value,
                "A20A61421B00000001000000056141",
                null,
                new CborOptions { Deterministic = true });
        }

        [Fact]
        public void UlongKeyedDictionaryKeysAreSortedWhenDeterministic()
        {
            Dictionary<ulong, string> value = new Dictionary<ulong, string>
            {
                [ulong.MaxValue] = "big",
                [1UL] = "small",
            };

            // Same wire bytes as CborObjectUlongMaxValueKeySortsAfterSmallKeyWhenDeterministic -- see
            // that test for the byte-by-byte derivation.
            Helper.TestWrite(value,
                "A20165736D616C6C1BFFFFFFFFFFFFFFFF63626967",
                null,
                new CborOptions { Deterministic = true });
        }

        // RFC 8949 4.2.1 requirement 1: "Integers must be as small as possible." CborWriter's existing
        // shortest-form ladder already does this unconditionally (it is not gated on Deterministic at
        // all) -- these pin that pre-existing behaviour so a future change cannot quietly widen it.
        // Major type 0 (unsigned), one-byte header 0x00-0x17 for values 0-23, then additional-info
        // 24/25/26 (prefix 0x18/0x19/0x1A) select the smallest argument width that still fits the value.
        [Theory]
        [InlineData(0, "00")]
        [InlineData(23, "17")]
        [InlineData(24, "1818")]
        [InlineData(255, "18FF")]
        [InlineData(256, "190100")]
        [InlineData(65536, "1A00010000")]
        public void IntegersUseShortestForm(int value, string expectedHex)
        {
            Helper.TestWrite(value, expectedHex, null, new CborOptions { Deterministic = true });
        }

        // RFC 8949 4.2.1 requirement 2: "the preferred serialization always uses the shortest form of
        // representing the argument." For floats this means trying binary16 (Half), then binary32
        // (Single), then binary64 (Double), taking the first that round-trips exactly -- CborWriter.
        // WriteSingle/WriteDouble already does this unconditionally. These pin the three tiers:
        //  - 1.5 is exactly representable in binary16 (Half), so it takes the 3-byte F9 form.
        //  - float.MaxValue needs the full 24-bit mantissa of binary32, so it takes the 5-byte FA form.
        //  - 1.1 is not exactly representable in binary16 or binary32, so it takes the 9-byte FB form.
        [Theory]
        [InlineData(1.5d, "F93E00")]                          // exactly representable as binary16
        [InlineData(3.4028234663852886E38d, "FA7F7FFFFF")]    // float.MaxValue: needs binary32
        [InlineData(1.1d, "FB3FF199999999999A")]               // needs binary64
        public void FloatsUsePreferredSerialization(double value, string expectedHex)
        {
            Helper.TestWrite(value, expectedHex, null, new CborOptions { Deterministic = true });
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        private class ArrayFormatObject
        {
            // Deliberately alphabetically out of order, same as OutOfOrderObject above -- Array
            // format is positional by MemberIndex, not by name, so unlike StringKeyMap/IntKeyMap there
            // is no name to sort by in the first place.
            [CborProperty(0)]
            public int Zebra { get; set; }
            [CborProperty(1)]
            public int Apple { get; set; }
            [CborProperty(2)]
            public int Mango { get; set; }
        }

        // RFC 8949 4.2.1 requirement 3: "The keys in every map must be sorted in the bytewise
        // lexicographic order of their deterministic encodings." CborObjectFormat.Array serializes
        // members positionally by MemberIndex instead of as a map, so there are no map keys at all --
        // nothing for the deterministic sort to do, and the output is already deterministic by
        // construction. Pinned two ways: the encoding matches the plain positional array regardless of
        // Deterministic, and two writes produce identical bytes.
        [Fact]
        public void ArrayFormatObjectsHaveNoKeysToSort()
        {
            CborOptions options = new CborOptions
            {
                Deterministic = true,
                ObjectFormat = CborObjectFormat.Array,
            };

            ArrayFormatObject value = new ArrayFormatObject { Zebra = 1, Apple = 2, Mango = 3 };

            // 83 array(3) 01 02 03 -- positions 0/1/2 are Zebra/Apple/Mango by MemberIndex, unaffected
            // by Deterministic since there are no keys to sort.
            Helper.TestWrite(value, "83010203", null, options);

            Assert.Equal(Helper.Write(value, options), Helper.Write(value, options));
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        private class ArrayFormatWithNegativeIndex
        {
            [CborProperty(-1)]
            public int Negative { get; set; }
            [CborProperty(0)]
            public int Zero { get; set; }
            [CborProperty(1)]
            public int One { get; set; }
        }

        // Array format writes members positionally and emits no keys at all, so the deterministic key
        // order has nothing to act on: any reordering it performed would move VALUES between array
        // positions, which changes what the document means. A negative MemberIndex is the case that
        // exposes it, because it is the one index whose deterministic order (major type 1, so last)
        // differs from the ascending-int order ObjectMapping already applied. Deterministic and
        // non-deterministic output must therefore be byte-identical here.
        [Fact]
        public void ArrayFormatWithNegativeIndexIsUnaffectedByDeterministic()
        {
            ArrayFormatWithNegativeIndex value = new ArrayFormatWithNegativeIndex { Negative = 7, Zero = 8, One = 9 };

            // 83 array(3) 07 08 09 -- positions follow ObjectMapping's ascending-index order (-1, 0, 1),
            // exactly as they do without Deterministic.
            Helper.TestWrite(value, "83070809", null, new CborOptions { Deterministic = true });

            Assert.Equal(
                Helper.Write(value),
                Helper.Write(value, new CborOptions { Deterministic = true }));
        }

        // The converter for a type is built once and cached in CborConverterRegistry, so any ordering
        // decision taken while building it would freeze whatever the flag happened to be at that moment.
        // CborOptions.Default is a process-wide singleton, which makes "flag set after something was
        // already serialized" the normal case rather than an exotic one. Deterministic output must
        // depend only on the flag's value at write time.
        [Fact]
        public void DeterministicTakesEffectWhenSetAfterTheConverterIsCached()
        {
            CborOptions options = new CborOptions();
            OutOfOrderObject value = new OutOfOrderObject { Zebra = 1, Apple = 2, Mango = 3 };

            // Builds and caches ObjectConverter<OutOfOrderObject> while Deterministic is still false.
            Assert.Equal("A3655A6562726101654170706C6502654D616E676F03", Helper.Write(value, options));

            options.Deterministic = true;

            // A3 map(3)
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            //   655A65627261 "Zebra"  01
            Assert.Equal("A3654170706C6502654D616E676F03655A6562726101", Helper.Write(value, options));
        }

        // Static so that it is not itself a member to serialize: the flip has to happen from inside the
        // write, and a property getter is the shortest way to get code to run there.
        private static CborOptions _optionsFlippedMidWrite;

        private class FlipsDeterministicWhileBeingWritten
        {
            public int Zebra { get; set; }

            public int Bob
            {
                // Writing this member turns the flag on partway through the map, between the header's
                // member count and the last member.
                get
                {
                    _optionsFlippedMidWrite.Deterministic = true;
                    return 9;
                }
                set { }
            }

            public int Apple { get; set; }
            public int Mango { get; set; }
        }

        // A write must run start to finish on one member ordering. The ordering is chosen from a flag
        // that anything running during the write can change -- a property getter as here, a custom
        // converter, or another thread sharing CborOptions.Default -- and the two orderings are
        // permutations of each other, so switching between them mid-map writes some members twice and
        // drops others while the map header still claims the original count. That document is
        // structurally corrupt and nothing downstream can tell: the count matches, every item parses.
        [Fact]
        public void FlippingDeterministicDuringAWriteDoesNotChangeThatWritesOrder()
        {
            CborOptions options = new CborOptions();
            _optionsFlippedMidWrite = options;

            FlipsDeterministicWhileBeingWritten value = new FlipsDeterministicWhileBeingWritten
            {
                Zebra = 1,
                Apple = 2,
                Mango = 3,
            };

            // The write started with the flag off, so it finishes in declaration order -- four distinct
            // members, in the order the header's count was computed for.
            //
            // A4 map(4)
            //   655A65627261 "Zebra"  01
            //   63426F62     "Bob"    09
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            Assert.Equal(
                "A4655A656272610163426F6209654170706C6502654D616E676F03",
                Helper.Write(value, options));

            // The flag really did flip, and the next write -- which starts after it -- is sorted:
            // "Bob" encodes shorter than the others, so it leads.
            //
            // A4 map(4)
            //   63426F62     "Bob"    09
            //   654170706C65 "Apple"  02
            //   654D616E676F "Mango"  03
            //   655A65627261 "Zebra"  01
            Assert.True(options.Deterministic);
            Assert.Equal(
                "A463426F6209654170706C6502654D616E676F03655A6562726101",
                Helper.Write(value, options));
        }

        // ... and back again: clearing the flag on options whose converters were built while it was set
        // must restore declaration order, so the flag is never a one-way latch.
        [Fact]
        public void ClearingDeterministicRestoresDeclarationOrder()
        {
            CborOptions options = new CborOptions { Deterministic = true };
            OutOfOrderObject value = new OutOfOrderObject { Zebra = 1, Apple = 2, Mango = 3 };

            Assert.Equal("A3654170706C6502654D616E676F03655A6562726101", Helper.Write(value, options));

            options.Deterministic = false;

            Assert.Equal("A3655A6562726101654170706C6502654D616E676F03", Helper.Write(value, options));
        }
    }
}
