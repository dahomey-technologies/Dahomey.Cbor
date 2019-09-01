using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Tests.Issues
{
    [TestClass]
    public class Issue0015
    {
        private class Tree
        {
            [CborProperty("age")]
            public readonly int _age;

            [CborIgnore]
            public long Age2 => 5;

            [CborConstructor]
            public Tree(int age)
            {
                _age = age;
            }
        }

        [TestMethod]
        public void TestLong()
        {
            const string hexBuffer = "A1636167650C";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.IsNotNull(tree);
            Assert.AreEqual(12, tree._age);
        }

        private class Tree2
        {
            [CborProperty("age")]
            public readonly int _age;

            [CborIgnore]
            public int Age2 => 5;

            [CborConstructor]
            public Tree2(int age)
            {
                _age = age;
            }
        }

        [TestMethod]
        public void TestInt()
        {
            const string hexBuffer = "A1636167650C";

            Tree2 tree = Helper.Read<Tree2>(hexBuffer);

            Assert.IsNotNull(tree);
            Assert.AreEqual(12, tree._age);
        }
    }
}
