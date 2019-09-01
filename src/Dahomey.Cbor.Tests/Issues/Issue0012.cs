using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{

    public class Issue0012
    {
        public class Tree
        {
            [CborProperty("name")]
            public string Name { get; }

            public Tree() { }

            public Tree(string name)
            {
                Name = name;
            }
        }

        [Fact]
        public void ReadReadOnlyProperty()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Null(tree.Name);
        }

        [Fact]
        public void WriteReadOnlyProperty()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Helper.TestWrite(new Tree("foo"), hexBuffer);
        }

        [Fact]
        public void ReadReadOnlyPropertyWithNonDefaultConstructor()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Tree>(om =>
            {
                om.AutoMap();
                om.MapCreator(t => new Tree(t.Name));
            });

            const string hexBuffer = "A1646E616D6563666F6F";

            Tree tree = Helper.Read<Tree>(hexBuffer, options);

            Assert.NotNull(tree);
            Assert.Equal("foo", tree.Name);
        }

        [Fact]
        public void WriteReadOnlyPropertyWithNonDefaultConstructor()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Tree>(om =>
            {
                om.AutoMap();
                om.MapCreator(t => new Tree(t.Name));
            });

            const string hexBuffer = "A1646E616D6563666F6F";

            Helper.TestWrite(new Tree("foo"), hexBuffer, null, options);
        }
    }
}
