using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class TupleTests
    {
        [Fact]
        public void ReadTuple()
        {
            const string hexBuffer = "82664C6F6E646F6E1907E7"; //["London", 2023]
            (string stringValue, int intValue) = Helper.Read<(string, int)>(hexBuffer);

            Assert.Equal("London", stringValue);
            Assert.Equal(2023, intValue);
        }

        /// <summary>
        /// A tuple behind a stack of semantic tags.
        /// </summary>
        /// <remarks>
        /// This converter reaches the reader below its tag-skipping entry points -- <c>ReadSize</c>
        /// skips no tag -- so it skips them itself, and it used to skip exactly one. Since #183 made
        /// <c>SkipSemanticTag</c> consume a whole stack, one skip left this the only converter that
        /// stops at the first tag, and a tuple behind two of them was rejected.
        /// <para>
        /// Asserted for the nullable alongside the underlying type, because that is where the gap was
        /// found: <c>NullableConverter</c> skipped a tag of its own before delegating, so a
        /// <c>(int, int)?</c> read these while a <c>(int, int)</c> did not.
        /// </para>
        /// </remarks>
        [Theory]
        [InlineData("820102")]
        [InlineData("C1820102")]
        [InlineData("C1C1820102")]
        [InlineData("C0C1D864820102")]
        // Indefinite-length, which takes the other arm of the size check.
        [InlineData("C1C19F0102FF")]
        public void ReadTupleThroughATagStack(string hexBuffer)
        {
            Assert.Equal((1, 2), Helper.Read<(int, int)>(hexBuffer));
            Assert.Equal(Helper.Read<(int, int)>(hexBuffer), Helper.Read<(int, int)?>(hexBuffer));
        }

        /// <summary>
        /// The shape that makes the above more than theoretical: an RFC 8949 §3.4.4 decimal fraction,
        /// which this library does not decode semantically and so surfaces as the two-element array it
        /// is encoded as. Under an outer tag it used to be rejected.
        /// </summary>
        [Theory]
        [InlineData("C48221196AB3")]
        [InlineData("C1C48221196AB3")]
        [InlineData("D864C48221196AB3")]
        public void ReadADecimalFractionAsATupleThroughATagStack(string hexBuffer)
        {
            // 273.15 as [-2, 27315]
            Assert.Equal((-2, 27315), Helper.Read<(int, int)>(hexBuffer));
            Assert.Equal(Helper.Read<(int, int)>(hexBuffer), Helper.Read<(int, int)?>(hexBuffer));
        }

        /// <summary>
        /// A tuple is an array, and nothing else decodes as one.
        /// </summary>
        /// <remarks>
        /// <c>ReadSize</c> takes the additional value off whatever header is current and does not ask
        /// which major type it belongs to, so every one of these used to yield an arity of 3 and read
        /// as <c>(1, 2, 3)</c>. The last three are the clearest: there is no container at all, just a
        /// head whose additional value happens to be 3, and the three bytes after it are the next data
        /// items in the document rather than anything belonging to this one.
        /// </remarks>
        [Theory]
        [InlineData("43010203")]    // bytes(3)
        [InlineData("63010203")]    // text(3)
        [InlineData("A3010203")]    // map(3)
        [InlineData("03010203")]    // unsigned 3
        [InlineData("23010203")]    // negative -4
        [InlineData("E3010203")]    // simple value 3
        // A tag in front changes nothing: the tags are stepped over and the item beneath is still
        // not an array.
        [InlineData("C143010203")]
        [InlineData("C1C1A3010203")]
        public void ANonArrayIsNotReadAsATuple(string hexBuffer)
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<(int, int, int)>(hexBuffer.HexToBytes()));

            Assert.Contains("Expected major type Array", exception.Message);
        }

        /// <summary>The check is in every arity, not only the one the report used.</summary>
        /// <remarks>
        /// Arities 2 to 7. <c>Tuple8Converter</c> carries the same change and cannot be reached
        /// through the public API to assert it: the provider hands it the <c>Rest</c> field's type as
        /// <c>T8</c>, while the converter declares <c>CborConverterBase&lt;(T1, …, T8)&gt;</c>, which
        /// C# expands to <c>ValueTuple&lt;T1, …, T7, ValueTuple&lt;T8&gt;&gt;</c> - a different type.
        /// So an 8-element tuple fails while building the converter for a one-element <c>Rest</c>, and
        /// a 9-element one fails casting the converter it did build.
        /// </remarks>
        [Fact]
        public void ANonArrayIsNotReadAsATupleOfAnyArity()
        {
            // 42 0102 -- bytes(2), against the arity-2 converter, and so on up
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int)>("420102".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int)>("43010203".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int)>("4401020304".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int)>("450102030405".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int, int)>("46010203040506".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int, int, int)>("4701020304050607".HexToBytes()));
        }

        /// <summary>The one document in the table above that is a tuple still reads as one.</summary>
        [Fact]
        public void AnArrayIsStillReadAsATuple()
        {
            Assert.Equal((1, 2, 3), Helper.Read<(int, int, int)>("83010203"));
        }

        public class TupleMember
        {
            [CborProperty("a")]
            public (int, int) A { get; set; }

            [CborProperty("b")]
            public int B { get; set; }
        }

        /// <summary>
        /// A wrong-shaped member is rejected where it sits, rather than read across the item boundary.
        /// </summary>
        /// <remarks>
        /// The arity check alone did not catch either of these: the byte string's additional value was
        /// the arity, so the converter read the tuple's items out of whatever followed the head. In
        /// <c>A261614201026162182A</c> the two content bytes happen to be two items, so the whole
        /// document read cleanly: <c>a</c> came back as <c>(1, 2)</c> and <c>b</c> as 42, with nothing
        /// to say the member had been encoded as a byte string. In <c>A2616142182A616207</c> they are
        /// one item - <c>18 2A</c> is 42 - so the second was taken from past the end of the byte
        /// string, and the *key* <c>"b"</c> was read as the tuple's second element.
        /// <para>
        /// Both now stop at the member, and <see cref="CborException.Path"/> names it: the failure is
        /// reported at <c>$.a</c>, not at <c>$.a[1]</c> inside a tuple that was never there.
        /// </para>
        /// </remarks>
        [Theory]
        // {"a": h'0102', "b": 42} -- content bytes that do parse as two items
        [InlineData("A261614201026162182A")]
        // {"a": h'182A', "b": 7}  -- content bytes that do not, so the read used to run past the item
        [InlineData("A2616142182A616207")]
        public void AWrongShapedMemberIsNotReadAcrossTheItemBoundary(string hexBuffer)
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<TupleMember>(hexBuffer.HexToBytes()));

            Assert.Contains("Expected major type Array", exception.Message);
            Assert.Equal("$.a", exception.Path);
        }

        /// <summary>
        /// A tuple of more than seven elements is one flat array of that many items.
        /// </summary>
        /// <remarks>
        /// C# represents such a tuple as seven fields plus a <c>Rest</c> holding the overflow, and that
        /// nesting is an implementation detail of the language rather than anything the format knows: a
        /// nine-element tuple is <c>[1, …, 9]</c>, not <c>[1, …, 7, [8, 9]]</c>. Fifteen elements nest
        /// twice — <c>Rest</c> is itself an eight-field tuple — and are still fifteen items.
        /// </remarks>
        [Fact]
        public void ATupleOfMoreThanSevenElementsIsOneFlatArray()
        {
            Helper.TestWrite((1, 2, 3, 4, 5, 6, 7, 8), "880102030405060708");
            Helper.TestWrite((1, 2, 3, 4, 5, 6, 7, 8, 9), "89010203040506070809");
            Helper.TestWrite((1, 2, 3, 4, 5, 6, 7, 8, 9, 10), "8A0102030405060708090A");
            Helper.TestWrite(
                (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15),
                "8F0102030405060708090A0B0C0D0E0F");
        }

        [Fact]
        public void ATupleOfMoreThanSevenElementsReadsBack()
        {
            Assert.Equal(
                (1, 2, 3, 4, 5, 6, 7, 8),
                Helper.Read<(int, int, int, int, int, int, int, int)>("880102030405060708"));
            Assert.Equal(
                (1, 2, 3, 4, 5, 6, 7, 8, 9),
                Helper.Read<(int, int, int, int, int, int, int, int, int)>("89010203040506070809"));
            Assert.Equal(
                (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15),
                Helper.Read<(int, int, int, int, int, int, int, int, int, int, int, int, int, int, int)>(
                    "8F0102030405060708090A0B0C0D0E0F"));
        }

        /// <summary>
        /// The items keep their order across the <c>Rest</c> boundary, which a tuple of one type cannot
        /// show: every element here is a different type, so a misordered read fails on major type.
        /// </summary>
        [Fact]
        public void TheItemsPastSevenKeepTheirOrder()
        {
            (int, string, bool, double, int, string, bool, int, string) value =
                (1, "two", true, 4.5, 5, "six", false, 8, "nine");

            string hexBuffer = Helper.Write(value);

            Assert.Equal(
                value,
                Helper.Read<(int, string, bool, double, int, string, bool, int, string)>(hexBuffer));
        }

        /// <summary>
        /// An arity past seven is refused for the same reasons a shorter one is, and the message names
        /// the flattened arity rather than the seven-plus-<c>Rest</c> shape behind it.
        /// </summary>
        [Fact]
        public void AWrongArityPastSevenIsRefused()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<(int, int, int, int, int, int, int, int, int)>(
                    "880102030405060708".HexToBytes()));

            Assert.Contains("Expected CBOR Array of size 9", exception.Message);

            // Too many, and the indefinite-length form of both.
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int, int, int, int)>(
                "89010203040506070809".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int, int, int, int, int)>(
                "9F0102030405060708FF".HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int, int, int, int)>(
                "9F010203040506070809FF".HexToBytes()));

            // And a non-array, which the arity past seven reaches through the same entry point.
            Assert.Throws<CborException>(() => Cbor.Deserialize<(int, int, int, int, int, int, int, int)>(
                "480102030405060708".HexToBytes()));
        }

        [Fact]
        public void AnIndefiniteLengthTuplePastSevenIsReadAndLeavesNothingBehind()
        {
            Assert.Equal(
                (1, 2, 3, 4, 5, 6, 7, 8, 9),
                Helper.Read<(int, int, int, int, int, int, int, int, int)>("9F010203040506070809FF"));

            // [[_ 1..9], [_ 1..9]] -- the second element only reads if the first consumed its break.
            List<(int, int, int, int, int, int, int, int, int)> list =
                Cbor.Deserialize<List<(int, int, int, int, int, int, int, int, int)>>(
                    ("82" + "9F010203040506070809FF" + "9F010203040506070809FF").HexToBytes());

            Assert.Equal(2, list.Count);
            Assert.Equal((1, 2, 3, 4, 5, 6, 7, 8, 9), list[1]);
        }

        /// <summary>
        /// A failure inside the overflow names the item's flattened position, so <c>$[8]</c> rather than
        /// a path through a <c>Rest</c> the document knows nothing about.
        /// </summary>
        [Fact]
        public void AnItemPastSevenIsNamedByItsFlattenedIndex()
        {
            // [1, 2, 3, 4, 5, 6, 7, 8, "x"] into nine ints
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<(int, int, int, int, int, int, int, int, int)>(
                    "89010203040506070861 78".Replace(" ", "").HexToBytes()));

            Assert.Equal("$[8]", exception.Path);
        }

        /// <summary>
        /// A one-element tuple, which C# has no literal for and which an eight-element tuple's
        /// <c>Rest</c> is. It is an array of one item like any other tuple.
        /// </summary>
        [Fact]
        public void AOneElementTupleIsAnArrayOfOne()
        {
            Helper.TestWrite(new ValueTuple<int>(1), "8101");
            Assert.Equal(new ValueTuple<int>(1), Helper.Read<ValueTuple<int>>("8101"));
        }

        public class LongTupleMember
        {
            [CborProperty("a")]
            public (int, int, int, int, int, int, int, int, int) A { get; set; }

            [CborProperty("b")]
            public int B { get; set; }
        }

        /// <summary>
        /// As a member and as a nullable, which reach the converter through
        /// <c>MemberConverter</c> and <c>NullableConverter</c> rather than directly.
        /// </summary>
        [Fact]
        public void ATuplePastSevenWorksAsAMemberAndAsANullable()
        {
            LongTupleMember member = Cbor.Deserialize<LongTupleMember>(
                ("A2" + "6161" + "89010203040506070809" + "6162" + "07").HexToBytes());

            Assert.Equal((1, 2, 3, 4, 5, 6, 7, 8, 9), member.A);
            Assert.Equal(7, member.B);

            Assert.Equal(
                (1, 2, 3, 4, 5, 6, 7, 8, 9),
                Helper.Read<(int, int, int, int, int, int, int, int, int)?>("89010203040506070809"));
            Assert.Null(Helper.Read<(int, int, int, int, int, int, int, int, int)?>("F6"));
        }

        [Fact]
        public void WriteTuple()
        {
            const string hexBuffer = "82664C6F6E646F6E1907E7"; //["London", 2023]
            string hexResult = Helper.Write(("London", 2023));

            Assert.Equal(hexBuffer, hexResult);
        }

        public class TupleObject
        {
            public int Int { get; set; }
            public (int, string) Tuple { get; set; }
            public string String { get; set; }
        }

        [Fact]
        public void ReadTupleObject()
        {
            // {"Int":12, "Tuple":[12, "foo"], "String": "foo"}
            const string hexBuffer = "A363496E740C655475706C65820C63666F6F66537472696E6763666F6F";

            TupleObject obj = Helper.Read<TupleObject>(hexBuffer);
            Assert.NotNull(obj);
            Assert.Equal(12, obj.Int);
            Assert.Equal(12, obj.Tuple.Item1);
            Assert.Equal("foo", obj.Tuple.Item2);
            Assert.Equal("foo", obj.String);
        }

        [Fact]
        public void WriteTupleObject()
        {
            // {"Int":12, "Tuple":[12, "foo"], "String": "foo"}
            const string hexBuffer = "A363496E740C655475706C65820C63666F6F66537472696E6763666F6F";

            TupleObject obj = new TupleObject
            {
                Int = 12,
                Tuple = (12, "foo"),
                String = "foo",
            };

            Helper.TestWrite(obj, hexBuffer);
        }
    }
}