using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dahomey.Cbor.Tests.Issues
{
    [TestClass]
    public class Issue0018
    {
        private class Tree
        {
            [CborProperty("NOTr")]
            private readonly byte[] _r;

            public byte[] R => _r;

            [CborConstructor("NOTr")]
            public Tree(byte[] r)
            {
                _r = r;
            }
        }
        [TestMethod]
        public void Test()
        {
            const string hexBuffer = "A1644E4F5472820102"; // {"NOTr": [1, 2]}

            Tree tree = Helper.Read<Tree>(hexBuffer); 

            Assert.IsNotNull(tree);
            Assert.IsNotNull(tree.R);
            Assert.AreEqual(2, tree.R.Length);
            Assert.AreEqual(1, tree.R[0]);
            Assert.AreEqual(2, tree.R[1]);
        }
    }
}
