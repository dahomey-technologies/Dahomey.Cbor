using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Pins <see cref="CborException.Path"/> and <see cref="Exception.Message"/> for one failure per
    /// shape of nesting a converter can add a segment for.
    /// </summary>
    /// <remarks>
    /// The path is built by the converters the exception passes through on its way up, and how a
    /// converter does that - a <c>catch</c> that rethrows, or an exception filter - is invisible to a
    /// caller. The expected values are literals rather than anything computed from the converters, so
    /// they hold whichever way it is done.
    /// </remarks>
    public class ExceptionPathStabilityTests
    {
        private class Item
        {
            public int A { get; set; }
        }

        private class Node
        {
            public Node Child { get; set; }
            public int Leaf { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyedNode
        {
            [CborProperty(1)]
            public IntKeyedNode Child { get; set; }

            [CborProperty(2)]
            public int Leaf { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        private class ArrayNode
        {
            [CborProperty(0)]
            public ArrayNode Child { get; set; }

            [CborProperty(1)]
            public int Leaf { get; set; }
        }

        private struct Point
        {
            public int X { get; set; }
        }

        private class PointHolder
        {
            public Point P { get; set; }
        }

        private class ListHolder
        {
            public List<Item> Items { get; set; }
        }

        private class ArrayHolder
        {
            public Item[] Items { get; set; }
        }

        private class ImmutableHolder
        {
            public ImmutableList<Item> Items { get; set; }
        }

        private class MapHolder
        {
            public Dictionary<string, Item> Map { get; set; }
        }

        private class IntMapHolder
        {
            public Dictionary<int, Item> Map { get; set; }
        }

        private class TupleHolder
        {
            public (int, string) T { get; set; }
        }

        private class BigTupleHolder
        {
            public (int, int, int, int, int, int, int, int, string) T { get; set; }
        }

        private class MixedHolder
        {
            public Dictionary<string, List<Item[]>> Data { get; set; }
        }

        private class TypedArrayHolder
        {
            public int[] V { get; set; }
        }

        private class TypedListHolder
        {
            public List<int> V { get; set; }
        }

        private class RequiredHolder
        {
            public RequiredMember Req { get; set; }
        }

        private class RequiredMember
        {
            [CborRequired]
            public int Needed { get; set; }
        }

        private class CreatorHolder
        {
            public Created C { get; set; }
        }

        private class Created
        {
            public int Id { get; }

            [CborConstructor]
            public Created(int id)
            {
                Id = id;
            }
        }

        private class DerivedHolder
        {
            public Base B { get; set; }
        }

        private class Base
        {
        }

        [CborDiscriminator("derived")]
        private class Derived : Base
        {
            public int A { get; set; }
        }

        private class InterceptedHolder
        {
            [CborConverter(typeof(InterceptingConverter))]
            public Item Body { get; set; }
        }

        /// <summary>
        /// A converter that catches, looks at the path as far as it is known, and rethrows - the
        /// pattern the documentation shows - between two layers of the library's own.
        /// </summary>
        private class InterceptingConverter : CborConverterBase<Item>
        {
            [ThreadStatic]
            public static string SeenOnTheWayUp;

            public override Item Read(ref CborReader reader)
            {
                try
                {
                    return CborOptions.Default.Registry.ConverterRegistry.Lookup<Item>().Read(ref reader);
                }
                catch (CborException exception)
                {
                    SeenOnTheWayUp = exception.Path;
                    exception.PrependPathMember("Inner");
                    throw;
                }
            }

            public override void Write(ref CborWriter writer, Item value)
            {
                throw new NotSupportedException();
            }
        }

        private static CborException Capture<T>(string hex, CborOptions options = null)
        {
            byte[] bytes = hex.HexToBytes();

            return Assert.Throws<CborException>(() => Cbor.Deserialize<T>(bytes, options));
        }

        private static void AssertPath(string path, string message, CborException exception)
        {
            Assert.Equal(path, exception.Path);
            Assert.Equal(message, exception.Message);
        }

        // ---- nested objects, in each of the three object formats ----

        [Fact]
        public void NestedClasses()
        {
            // a1 65 4368696c64        {"Child":
            //    a1 65 4368696c64        {"Child":
            //       a1 64 4c656166 61 78    {"Leaf": "x"}}}
            CborException exception = Capture<Node>("A1654368696C64A1654368696C64A1644C6561666178");

            AssertPath("$.Child.Child.Leaf",
                "[21] Invalid major type TextString. Failed to deserialize from \"$.Child.Child.Leaf\".",
                exception);
        }

        [Fact]
        public void NestedClassesWithIntegerKeys()
        {
            // a1 01 a1 01 a1 02 61 78     {1: {1: {2: "x"}}}
            CborException exception = Capture<IntKeyedNode>("A101A101A1026178");

            AssertPath("$.Child.Child.Leaf",
                "[7] Invalid major type TextString. Failed to deserialize from \"$.Child.Child.Leaf\".",
                exception);
        }

        [Fact]
        public void NestedClassesInArrayFormat()
        {
            // 82              [
            //    82 f6 61 78      [null, "x"],
            //    00           0]
            CborException exception = Capture<ArrayNode>("8282F6617800");

            AssertPath("$.Child.Leaf",
                "[4] Invalid major type TextString. Failed to deserialize from \"$.Child.Leaf\".",
                exception);
        }

        [Fact]
        public void AStructMember()
        {
            // a1 61 50 a1 61 58 61 78     {"P": {"X": "x"}}
            CborException exception = Capture<PointHolder>("A16150A161586178");

            AssertPath("$.P.X",
                "[7] Invalid major type TextString. Failed to deserialize from \"$.P.X\".",
                exception);
        }

        // ---- collections ----

        [Fact]
        public void AListItem()
        {
            // a1 65 4974656d73 82 a1 61 41 01 a1 61 41 61 78     {"Items": [{"A": 1}, {"A": "x"}]}
            CborException exception = Capture<ListHolder>("A1654974656D7382A1614101A161416178");

            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                exception);
        }

        [Fact]
        public void AListItemOfAnIndefiniteLengthArray()
        {
            // a1 65 4974656d73 9f a1 61 41 01 a1 61 41 61 78 ff     {"Items": [_ {"A": 1}, {"A": "x"}]}
            CborException exception = Capture<ListHolder>("A1654974656D739FA1614101A161416178FF");

            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                exception);
        }

        [Fact]
        public void AnArrayItem()
        {
            CborException exception = Capture<ArrayHolder>("A1654974656D7382A1614101A161416178");

            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                exception);
        }

        [Fact]
        public void AnImmutableListItem()
        {
            CborException exception = Capture<ImmutableHolder>("A1654974656D7382A1614101A161416178");

            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                exception);
        }

        [Fact]
        public void ADictionaryValue()
        {
            // a1 63 4d6170 a1 61 6b a1 61 41 61 78     {"Map": {"k": {"A": "x"}}}
            CborException exception = Capture<MapHolder>("A1634D6170A1616BA161416178");

            AssertPath("$.Map.k.A",
                "[12] Invalid major type TextString. Failed to deserialize from \"$.Map.k.A\".",
                exception);
        }

        [Fact]
        public void ADictionaryValueUnderAnIntegerKey()
        {
            // a1 63 4d6170 a1 07 a1 61 41 61 78     {"Map": {7: {"A": "x"}}}
            CborException exception = Capture<IntMapHolder>("A1634D6170A107A161416178");

            AssertPath("$.Map.7.A",
                "[11] Invalid major type TextString. Failed to deserialize from \"$.Map.7.A\".",
                exception);
        }

        [Fact]
        public void ADictionaryKeyThatIsNotAPlainName()
        {
            // a1 63 4d6170 a1 63 612e62 a1 61 41 61 78     {"Map": {"a.b": {"A": "x"}}}
            CborException exception = Capture<MapHolder>("A1634D6170A163612E62A161416178");

            AssertPath("$.Map['a.b'].A",
                "[14] Invalid major type TextString. Failed to deserialize from \"$.Map['a.b'].A\".",
                exception);
        }

        [Fact]
        public void ATupleItem()
        {
            // a1 61 54 82 01 02     {"T": [1, 2]}, where the second item must be a text string
            CborException exception = Capture<TupleHolder>("A16154820102");

            AssertPath("$.T[1]",
                "[6] Expected major type TextString (3). Failed to deserialize from \"$.T[1]\".",
                exception);
        }

        [Fact]
        public void ATupleItemPastTheSeventh()
        {
            // a1 61 54 89 01 02 03 04 05 06 07 08 09     {"T": [1, 2, 3, 4, 5, 6, 7, 8, 9]}
            // where the ninth item must be a text string
            CborException exception = Capture<BigTupleHolder>("A1615489010203040506070809");

            AssertPath("$.T[8]",
                "[13] Expected major type TextString (3). Failed to deserialize from \"$.T[8]\".",
                exception);
        }

        [Fact]
        public void EveryKindOfCollectionInOnePath()
        {
            // {"Data": {"k": [[{"A": 1}, {"A": "x"}]]}}
            CborException exception = Capture<MixedHolder>("A16444617461A1616B8182A1614101A161416178");

            AssertPath("$.Data.k[0][1].A",
                "[19] Invalid major type TextString. Failed to deserialize from \"$.Data.k[0][1].A\".",
                exception);
        }

        // ---- the failure is on the container itself ----

        [Fact]
        public void AListThatIsNotAnArray()
        {
            // a1 65 4974656d73 01     {"Items": 1}
            CborException exception = Capture<ListHolder>("A1654974656D7301");

            AssertPath("$.Items",
                "[8] Expected major type Array (4). Failed to deserialize from \"$.Items\".",
                exception);
        }

        [Fact]
        public void ADictionaryThatIsNotAMap()
        {
            // a1 63 4d6170 01     {"Map": 1}
            CborException exception = Capture<MapHolder>("A1634D617001");

            AssertPath("$.Map",
                "[6] Expected major type Map (5). Failed to deserialize from \"$.Map\".",
                exception);
        }

        [Fact]
        public void ARequiredMemberMissingFromANestedObject()
        {
            // a1 63 526571 a0     {"Req": {}}
            CborException exception = Capture<RequiredHolder>("A163526571A0");

            AssertPath("$.Req",
                "Required property 'Needed' not found in JSON. Failed to deserialize from \"$.Req\".",
                exception);
        }

        [Fact]
        public void ACreatorValueOfTheWrongType()
        {
            // a1 61 43 a1 62 4964 61 78     {"C": {"Id": "x"}}
            CborException exception = Capture<CreatorHolder>("A16143A16249646178");

            AssertPath("$.C.Id",
                "[8] Invalid major type TextString. Failed to deserialize from \"$.C.Id\".",
                exception);
        }

        [Fact]
        public void APolymorphicMember()
        {
            // a1 61 42 a2 62 5f74 67 64657269766564 61 41 61 78     {"B": {"_t": "derived", "A": "x"}}
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Derived>();

            CborException exception = Capture<DerivedHolder>("A16142A2625F74676465726976656461416178", options);

            AssertPath("$.B.A",
                "[18] Invalid major type TextString. Failed to deserialize from \"$.B.A\".",
                exception);
        }

        // ---- the root, and a typed array ----

        [Fact]
        public void TheRootOfAnObject()
        {
            CborException exception = Capture<Item>("01");

            AssertPath("$",
                "[1] Expected major type Map (5). Failed to deserialize from \"$\".",
                exception);
        }

        [Fact]
        public void TheRootOfAList()
        {
            CborException exception = Capture<List<Item>>("01");

            AssertPath("$",
                "[1] Expected major type Array (4). Failed to deserialize from \"$\".",
                exception);
        }

        [Fact]
        public void TheRootOfAnArray()
        {
            CborException exception = Capture<Item[]>("01");

            AssertPath("$",
                "[1] Expected major type Array (4). Failed to deserialize from \"$\".",
                exception);
        }

        [Fact]
        public void TheRootOfADictionary()
        {
            CborException exception = Capture<Dictionary<string, Item>>("01");

            AssertPath("$",
                "[1] Expected major type Map (5). Failed to deserialize from \"$\".",
                exception);
        }

        [Fact]
        public void TheRootOfATuple()
        {
            CborException exception = Capture<(int, string)>("01");

            AssertPath("$",
                "[1] Expected major type Array (4). Failed to deserialize from \"$\".",
                exception);
        }

        [Fact]
        public void AnItemOfARootList()
        {
            // 82 a1 61 41 01 a1 61 41 61 78     [{"A": 1}, {"A": "x"}]
            CborException exception = Capture<List<Item>>("82A1614101A161416178");

            AssertPath("$[1].A",
                "[9] Invalid major type TextString. Failed to deserialize from \"$[1].A\".",
                exception);
        }

        [Fact]
        public void ATypedArrayWhosePayloadIsNotAWholeNumberOfItems()
        {
            // a1 61 56 d8 4e 43 01 02 03     {"V": 78(h'010203')}, where an int needs four bytes
            CborOptions options = new CborOptions { TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian };
            CborException exception = Capture<TypedArrayHolder>("A16156D84E43010203", options);

            AssertPath("$.V",
                "Typed array payload of 3 bytes is not a multiple of the 4 byte element size. Failed to deserialize from \"$.V\".",
                exception);
        }

        [Fact]
        public void ATypedArrayReadIntoAList()
        {
            CborOptions options = new CborOptions { TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian };
            CborException exception = Capture<TypedListHolder>("A16156D84E43010203", options);

            AssertPath("$.V",
                "Typed array payload of 3 bytes is not a multiple of the 4 byte element size. Failed to deserialize from \"$.V\".",
                exception);
        }

        [Fact]
        public void ARootTypedArray()
        {
            CborOptions options = new CborOptions { TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian };
            CborException exception = Capture<int[]>("D84E43010203", options);

            AssertPath("$",
                "Typed array payload of 3 bytes is not a multiple of the 4 byte element size. Failed to deserialize from \"$\".",
                exception);
        }

        // ---- a converter of the caller's, between the library's ----

        [Fact]
        public void ACallerCatchSeesThePathSoFarAndAddsToIt()
        {
            // a1 64 426f6479 a1 61 41 61 78     {"Body": {"A": "x"}}
            InterceptingConverter.SeenOnTheWayUp = null;

            CborException exception = Capture<InterceptedHolder>("A164426F6479A161416178");

            Assert.Equal("$.A", InterceptingConverter.SeenOnTheWayUp);
            AssertPath("$.Body.Inner.A",
                "[10] Invalid major type TextString. Failed to deserialize from \"$.Body.Inner.A\".",
                exception);
        }

        // ---- every way into the reader ----

        [Fact]
        public async Task TheSamePathFromEveryEntryPointAsync()
        {
            byte[] bytes = "A1654974656D7382A1614101A161416178".HexToBytes();

            CborException fromSpan = Assert.Throws<CborException>(() => Cbor.Deserialize<ListHolder>(bytes));
            CborException fromSequence = Assert.Throws<CborException>(
                () => Cbor.Deserialize<ListHolder>(new ReadOnlySequence<byte>(bytes)));
            CborException fromStream = await Assert.ThrowsAsync<CborException>(
                async () => await Cbor.DeserializeAsync<ListHolder>(new NonMemoryStream(bytes)));

            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                fromSpan);
            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                fromSequence);
            AssertPath("$.Items[1].A",
                "[16] Invalid major type TextString. Failed to deserialize from \"$.Items[1].A\".",
                fromStream);
        }

        /// <summary>A stream that is not a <see cref="MemoryStream"/>, so the read goes through the buffered path.</summary>
        private class NonMemoryStream : Stream
        {
            private readonly MemoryStream _inner;

            public NonMemoryStream(byte[] bytes)
            {
                _inner = new MemoryStream(bytes, writable: false);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
