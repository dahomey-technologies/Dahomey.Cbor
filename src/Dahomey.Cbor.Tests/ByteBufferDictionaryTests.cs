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
        /// A key that merely passes through the nodes of a longer one is not a duplicate of it: the
        /// segments it shares are the way to an entry, not an entry.
        /// </summary>
        [Fact]
        public void AddingAPrefixOfAnExistingKeyIsNotADuplicate()
        {
            ByteBufferDictionary<int> dictionary = new ByteBufferDictionary<int>();
            dictionary.Add(Encoding.UTF8.GetBytes("PropertyAlpha"), 12);
            dictionary.Add(Encoding.UTF8.GetBytes("Property"), 13);

            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes("PropertyAlpha"), out int alpha));
            Assert.Equal(12, alpha);
            Assert.True(dictionary.TryGetValue(Encoding.UTF8.GetBytes("Property"), out int property));
            Assert.Equal(13, property);
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
