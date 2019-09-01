using Dahomey.Cbor.Attributes;
using Xunit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Tests.Issues
{

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

        [Fact]
        public void TestLong()
        {
            const string hexBuffer = "A1636167650C";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Equal(12, tree._age);
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

        [Fact]
        public void TestInt()
        {
            const string hexBuffer = "A1636167650C";

            Tree2 tree = Helper.Read<Tree2>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Equal(12, tree._age);
        }
    }
}
