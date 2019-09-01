using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{

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
        [Fact]
        public void Test()
        {
            const string hexBuffer = "A1644E4F5472820102"; // {"NOTr": [1, 2]}

            Tree tree = Helper.Read<Tree>(hexBuffer); 

            Assert.NotNull(tree);
            Assert.NotNull(tree.R);
            Assert.Equal(2, tree.R.Length);
            Assert.Equal(1, tree.R[0]);
            Assert.Equal(2, tree.R[1]);
        }
    }
}
