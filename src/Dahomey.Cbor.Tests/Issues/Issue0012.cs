using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dahomey.Cbor.Tests.Issues
{
    [TestClass]
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

        [TestMethod]
        public void ReadReadOnlyProperty()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.IsNotNull(tree);
            Assert.AreEqual(null, tree.Name);
        }

        [TestMethod]
        public void WriteReadOnlyProperty()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Helper.TestWrite(new Tree("foo"), hexBuffer);
        }

        [TestMethod]
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

            Assert.IsNotNull(tree);
            Assert.AreEqual("foo", tree.Name);
        }

        [TestMethod]
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
