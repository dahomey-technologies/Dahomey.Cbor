using Dahomey.Cbor.Tests.Extensions;
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