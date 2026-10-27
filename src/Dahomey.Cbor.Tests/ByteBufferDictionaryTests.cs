using Dahomey.Cbor.Util;
using System;
using System.Text;
using Xunit;

namespace Dahomey.Cbor.Tests
{

    public class ByteBufferDictionaryTests
    {
        [Theory]
        [InlineData("short1,short2")]
        [InlineData("longvalue1,longvalue2")]
        [InlineData("longvalue1,short1,longvalue2,short2")]
        public void AddTryGet(string values)
        {
            ByteBufferDictionary<string> binaryTree = new ByteBufferDictionary<string>();
            string[] valuesArray = values.Split(',');

            foreach(string value in valuesArray)
            {
                binaryTree.Add(value.AsBinarySpan(), value);
            }

            foreach (string value in valuesArray)
            {
                bool success = binaryTree.TryGetValue(value.AsBinarySpan(), out string actualValue);
                Assert.True(success);
                Assert.Equal(value, actualValue);
            }
        }

        /// <summary>
        /// A key is stored one node per eight bytes, so a longer key builds nodes for its prefixes on
        /// the way. Those are not entries, and looking one up must miss rather than answer with the
        /// default value of a stored one.
        /// </summary>
        /// <remarks>
        /// What it answered instead was <c>default(T)</c> with <c>true</c>, which for a member lookup
        /// is a null converter — a <see cref="System.NullReferenceException"/> from a document that
        /// merely used a key eight bytes long.
        /// </remarks>
        [Fact]
        public void APrefixOfALongerKeyIsNotFound()
        {
            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(Encoding.UTF8.GetBytes("PropertyAlpha"), 12);

            Assert.False(dictionary.TryGetValue(Encoding.UTF8.GetBytes("Property"), out int _));
            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes("PropertyAlpha"), out int found));
            Assert.Equal(12, found);
        }

        /// <summary>
        /// The empty key is a key like any other: <c>[CborProperty("")]</c> names a member with it.
        /// </summary>
        /// <remarks>
        /// It has no eight-byte segment, so it is stored on the root node, which the lookup never
        /// looked at: the entry was added and could not be found.
        /// </remarks>
        [Fact]
        public void TheEmptyKeyIsFound()
        {
            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            Assert.False(dictionary.TryGetValue(ReadOnlySpan<byte>.Empty, out int _));

            dictionary.Add(Encoding.UTF8.GetBytes("short"), 1);
            dictionary.Add(ReadOnlySpan<byte>.Empty, 2);
            dictionary.Add(Encoding.UTF8.GetBytes("longervaluethaneightbytes"), 3);

            Assert.True(dictionary.TryGetValue(ReadOnlySpan<byte>.Empty, out int found));
            Assert.Equal(2, found);
            Assert.False(dictionary.TryGetValue(new byte[] { 0 }, out int _));
            Assert.Throws<ArgumentException>(() => dictionary.Add(ReadOnlySpan<byte>.Empty, 4));
        }

        /// <summary>
        /// A key and the same key followed by zero bytes are different keys.
        /// </summary>
        /// <remarks>
        /// A segment shorter than eight bytes is padded with zeros, so both ended on the same node and
        /// a document key "a\0" read into the member "a".
        /// </remarks>
        [Theory]
        [InlineData("a")]
        [InlineData("eightchr")]
        [InlineData("longervaluethaneightbytes")]
        public void TrailingZeroBytesAreNotIgnored(string key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(key);
            byte[] padded = Encoding.UTF8.GetBytes(key + "\0");

            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(bytes, 12);

            Assert.False(dictionary.TryGetValue(padded, out int _));
            Assert.True(dictionary.TryGetValue(bytes, out int found));
            Assert.Equal(12, found);

            // the same the other way round
            dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(padded, 13);

            Assert.False(dictionary.TryGetValue(bytes, out int _));
            Assert.True(dictionary.TryGetValue(padded, out found));
            Assert.Equal(13, found);
        }

        /// <summary>
        /// Adding a key twice is refused rather than silently replacing the first entry.
        /// </summary>
        /// <remarks>
        /// Every caller builds a lookup once from keys it believes distinct, so a repeat is a mistake
        /// upstream — two members mapped to one CBOR name, issue #177 — and the entry it overwrote was
        /// a member that could then never be read.
        /// </remarks>
        [Theory]
        [InlineData("short1")]
        [InlineData("eightchr")]
        [InlineData("longervaluethaneightbytes")]
        public void AddingTheSameKeyTwiceThrows(string key)
        {
            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(Encoding.UTF8.GetBytes(key), 12);

            ArgumentException ex = Assert.Throws<ArgumentException>(
                () => dictionary.Add(Encoding.UTF8.GetBytes(key), 13));

            Assert.Contains(key, ex.Message);

            // and the entry that was there is the one that is still there
            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes(key), out int found));
            Assert.Equal(12, found);
        }

        /// <summary>
        /// Sharing segments with an existing key is not being a duplicate of it, in either direction:
        /// a key laid down through the nodes of an entry already there, and an entry already there
        /// lying on the way to a longer key.
        /// </summary>
        /// <remarks>
        /// The longer-then-shorter order is the one <see cref="APrefixThatIsAlsoAKeyIsFound"/> covers.
        /// Shorter first is what puts an entry on the path the longer key must extend, so it is the
        /// order that would trip a duplicate check looking at the nodes it passes rather than the one
        /// it lands on.
        /// </remarks>
        [Fact]
        public void SharingSegmentsWithAnExistingKeyIsNotADuplicate()
        {
            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(Encoding.UTF8.GetBytes("Property"), 13);
            dictionary.Add(Encoding.UTF8.GetBytes("PropertyAlpha"), 12);

            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes("Property"), out int property));
            Assert.Equal(13, property);
            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes("PropertyAlpha"), out int alpha));
            Assert.Equal(12, alpha);
        }

        /// <summary>And a prefix that was itself added is found, being an entry in its own right.</summary>
        [Fact]
        public void APrefixThatIsAlsoAKeyIsFound()
        {
            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(Encoding.UTF8.GetBytes("PropertyAlpha"), 12);
            dictionary.Add(Encoding.UTF8.GetBytes("Property"), 13);

            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes("Property"), out int found));
            Assert.Equal(13, found);
        }
    }
}
