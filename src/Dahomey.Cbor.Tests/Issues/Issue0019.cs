using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0019
    {
        public class Tree : Plant
        {
            [CborProperty("age")]
            public int Age { get; }

            [CborConstructor]
            public Tree(int age, string name) : base(name)
            {
                Age = age;
            }
        }

        public abstract class Plant
        {
            [CborProperty("name")]
            public string Name { get; }

            public Plant(string name)
            {
                Name = name;
            }
        }

        [Fact]
        public void TestWrite()
        {
            Tree tree = new Tree(12, "foo");

            const string hexBuffer = "A2636167650C646E616D6563666F6F"; // { "name": "foo", "age": 12 }
            Helper.TestWrite(tree, hexBuffer);
        }

        [Fact]
        public void TestRead()
        {
            const string hexBuffer = "A2636167650C646E616D6563666F6F"; // { "name": "foo", "age": 12 }
            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Equal("foo", tree.Name);
            Assert.Equal(12, tree.Age);
        }
    }
}
