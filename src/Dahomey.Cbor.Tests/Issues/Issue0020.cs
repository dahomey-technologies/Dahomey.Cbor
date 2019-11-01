using Dahomey.Cbor.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0020
    {
        public class Tree
        {
            [CborProperty("age")]
            public readonly int _age;

            [CborProperty("fruit")]
            public Fruit _fruit;

            [CborConstructor]
            public Tree(int age, Fruit fruit)
            {
                _age = age;
                _fruit = fruit;
            }
        }

        public abstract class Fruit
        {
            public string id2 = "myid"; // if I remove this it won't throw
        }

        [CborDiscriminator("Apple")]
        public class Apple : Fruit
        {
            [CborProperty("type")]
            public const string type = "I am an apple";
        }

        [CborDiscriminator("Orange")]
        public class Orange : Fruit
        {
            [CborProperty("type")]
            public const string type = "I am an orange";
        }

        [Fact]
        public void TestWrite()
        {
            CborOptions options = new CborOptions();
            options.Registry.RegisterType(typeof(Apple));
            options.Registry.RegisterType(typeof(Orange));

            Tree tree = new Tree(10, new Apple());

            const string hexBuffer = "A2636167650A656672756974A3625F74654170706C6564747970656D4920616D20616E206170706C6563696432646D796964";
            Helper.TestWrite(tree, hexBuffer, null, options);
        }

        [Fact]
        public void TestRead()
        {
            CborOptions options = new CborOptions();
            options.Registry.RegisterType(typeof(Apple));
            options.Registry.RegisterType(typeof(Orange));

            const string hexBuffer = "A2636167650A656672756974A3625F74654170706C6564747970656D4920616D20616E206170706C6563696432646D796964";
            Tree tree = Helper.Read<Tree>(hexBuffer, options);

            Assert.NotNull(tree);
            Assert.Equal(10, tree._age);
            Assert.NotNull(tree._fruit);
            Assert.IsType<Apple>(tree._fruit);
            Assert.Equal("myid", ((Apple)tree._fruit).id2);
        }
    }
}
