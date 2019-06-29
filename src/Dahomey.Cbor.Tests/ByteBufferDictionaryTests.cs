using Dahomey.Cbor.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class ByteBufferDictionaryTests
    {
        [DataTestMethod]
        [DataRow("short1,short2")]
        [DataRow("longvalue1,longvalue2")]
        [DataRow("longvalue1,short1,longvalue2,short2")]
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
                Assert.IsTrue(success);
                Assert.AreEqual(value, actualValue);
            }
        }
    }
}
