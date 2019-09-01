using Dahomey.Cbor.Util;
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
    }
}
