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