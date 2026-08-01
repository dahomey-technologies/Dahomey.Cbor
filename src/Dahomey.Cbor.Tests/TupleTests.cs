using Dahomey.Cbor.Attributes;
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

        public class TaggedTupleObject
        {
            public (int, int) T { get; set; }
        }

        [Fact]
        public void ReadTaggedTupleMember()
        {
            // Tuple converters reach the reader below its tag-skipping entry points, so they skip
            // their own tag. An irrelevant tag in front of a tuple is ignored, as CBOR requires.
            // A1 6154 "T" C1 tag(1) 820102 [1, 2]
            TaggedTupleObject obj = Helper.Read<TaggedTupleObject>("A16154C1820102");
            Assert.Equal((1, 2), obj.T);

            // A1 6154 "T" 820102 [1, 2] -- the tuple is written back with no tag of its own
            Helper.TestWrite(obj, "A16154820102");
            Assert.Equal((1, 2), Helper.Read<TaggedTupleObject>("A16154820102").T);
        }

        [Fact]
        public void ReadTaggedTupleAtRoot()
        {
            // C1 tag(1) 820102 [1, 2]
            Assert.Equal((1, 2), Helper.Read<(int, int)>("C1820102"));
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class TupleRange
        {
            [CborProperty(1)]
            public (int, int) Range { get; set; }
        }

        [CborDiscriminator("Named")]
        [CborObjectFormat(CborObjectFormat.Array)]
        public class NamedTupleRange : TupleRange
        {
            [CborProperty(2)]
            public string Name { get; set; }
        }

        [Fact]
        public void ReadArrayFormatObjectWhoseFirstItemIsATaggedTuple()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<NamedTupleRange>();

            // The object looks for a discriminator tag as its first item and finds a tag that belongs
            // to the tuple instead, which therefore has to reach the tuple's own converter.
            // 82 array(2) C1 tag(1) 820102 [1, 2] 63666F6F "foo"
            NamedTupleRange obj = Helper.Read<NamedTupleRange>("82C182010263666F6F", options);
            Assert.Equal((1, 2), obj.Range);
            Assert.Equal("foo", obj.Name);

            // Both tags at once, nested.
            // 83 array(3) D827 tag(39) 654E616D6564 "Named" C1 tag(1) 820102 [1, 2] 63666F6F "foo"
            TupleRange polymorphic = Helper.Read<TupleRange>("83D827654E616D6564C182010263666F6F", options);
            NamedTupleRange named = Assert.IsType<NamedTupleRange>(polymorphic);
            Assert.Equal((1, 2), named.Range);
            Assert.Equal("foo", named.Name);
        }
    }
}