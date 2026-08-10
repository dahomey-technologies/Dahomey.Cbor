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