using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{

    public class Issue0017
    {
        private class Tree
        {
            public int age = 0;

            [CborProperty("r")]
            private readonly byte[] _r;

            [CborProperty("s")]
            private readonly byte[] _s;

            [CborConstructor]
            public Tree(byte[] r, byte[] s)
            {
                _r = r;
                _s = s;
            }

            public byte[] GetR()
            {
                return _r;
            }

            public byte[] GetS()
            {
                return _s;
            }
        }

        [Fact]
        public void Test()
        {
            CborOptions.Default.Registry.ObjectMappingRegistry.Register<Tree>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(t => t.age);
            });

            const string hexBuffer = "A1636167650C";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Equal(12, tree.age);
            Assert.Null(tree.GetR());
            Assert.Null(tree.GetS());
        }
    }
}
