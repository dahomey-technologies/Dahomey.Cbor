using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// https://github.com/dahomey-technologies/Dahomey.Cbor/issues/3
    /// </summary>
    [TestClass]
    public class Issue0003
    {
        public enum TreeType
        {
            Type0,
            Type1,
            Type2
        }

        public class Tree
        {
            [CborProperty("name")]
            public string Name { get; set; }

            [CborProperty("age")]
            [CborIgnore]
            public int Age { get; set; }

            [CborProperty("type")]
            public TreeType Type { get; set; }

            public Tree() { }
        }

        [TestMethod]
        public void Test()
        {
            Tree value = new Tree
            {
                Name = "Tree",
                Age = 12,
                Type = TreeType.Type1
            };

            const string expectedHexBuffer = "A2646E616D656454726565647479706501";
            Helper.TestWrite(value, expectedHexBuffer);
        }
    }
}
