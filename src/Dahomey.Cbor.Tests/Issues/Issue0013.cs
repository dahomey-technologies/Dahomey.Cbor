using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// https://github.com/dahomey-technologies/Dahomey.Cbor/issues/13
    /// </summary>
    [TestClass]
    public class Issue0013
    {
        public class Tree
        {
            [CborProperty("name")]
            public readonly string _name;

            public Tree() { }

            public Tree(string name)
            {
                this._name = name;
            }
        }
        
        [TestMethod]
        public void ReadReadOnlyField()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.IsNotNull(tree);
            Assert.AreEqual(null, tree._name);
        }

        [TestMethod]
        public void WriteReadOnlyField()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Helper.TestWrite(new Tree("foo"), hexBuffer);
        }

        [TestMethod]
        public void ReadReadOnlyFieldWithNonDefaultConstructor()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Tree>(om =>
            {
                om.AutoMap();
                om.MapCreator(t => new Tree(t._name))
                    .SetMemberNames("name");
            });

            const string hexBuffer = "A1646E616D6563666F6F";

            Tree tree = Helper.Read<Tree>(hexBuffer, options);

            Assert.IsNotNull(tree);
            Assert.AreEqual("foo", tree._name);
        }

        [TestMethod]
        public void WriteReadOnlyFieldWithNonDefaultConstructor()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Tree>(om =>
            {
                om.AutoMap();
                om.MapCreator(t => new Tree(t._name))
                    .SetMemberNames("name");
            });

            const string hexBuffer = "A1646E616D6563666F6F";

            Helper.TestWrite(new Tree("foo"), hexBuffer, null, options);
        }

        public class Tree2
        {
            [CborProperty("name")]
            public readonly string _name;

            [CborConstructor("name")]
            public Tree2(string name)
            {
                this._name = name;
            }
        }

        [TestMethod]
        public void ReadReadOnlyFieldWithCborContructor()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Tree2 tree = Helper.Read<Tree2>(hexBuffer);

            Assert.IsNotNull(tree);
            Assert.AreEqual("foo", tree._name);
        }

        [TestMethod]
        public void WriteReadOnlyFieldWithCborConstructor()
        {
            const string hexBuffer = "A1646E616D6563666F6F";

            Helper.TestWrite(new Tree2("foo"), hexBuffer);
        }
    }
}
