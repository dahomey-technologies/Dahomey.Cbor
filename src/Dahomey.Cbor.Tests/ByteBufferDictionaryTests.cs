using Dahomey.Cbor.Util;
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
