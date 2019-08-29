using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// [bug] deserialization failing if get only property and const/static field are used alongside #14
    /// </summary>
    /// https://github.com/dahomey-technologies/Dahomey.Cbor/issues/14
    [TestClass]
    public class Issue0014
    {
        private class Tree
        {
            [CborProperty("age")]
            public int Age { get; }

            [CborProperty("id")]
            private const string id = "123";

            [CborConstructor]
            public Tree(int age)
            {
                Age = age;
            }
        }

        [TestMethod]
        public void Read()
        {
            const string hexBuffer = "A2636167650C62696463313233";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.IsNotNull(tree);
            Assert.AreEqual(12, tree.Age);
        }

        [TestMethod]
        public void Write()
        {
            const string hexBuffer = "A2636167650C62696463313233";

            Helper.TestWrite(new Tree(12), hexBuffer);
        }
    }
}
