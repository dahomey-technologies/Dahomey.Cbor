using Dahomey.Cbor.Attributes;
using Xunit;
using System.Reflection;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// https://github.com/dahomey-technologies/Dahomey.Cbor/issues/16
    /// </summary>

    public class Issue0016
    {
        public class Tree
        {
            private int _age;
            private string _name;

            public int GetAge() => _age;
            public string GetName() => _name;
        }

        [Fact]
        public void TestByApi()
        {
            CborOptions.Default.Registry.ObjectMappingRegistry.Register<Tree>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(Tree).GetField("_age", BindingFlags.Instance | BindingFlags.NonPublic))
                    .SetMemberName("age");
                om.MapMember(typeof(Tree).GetField("_name", BindingFlags.Instance | BindingFlags.NonPublic))
                    .SetMemberName("name");
            });

            const string hexBuffer = "A2636167650C646E616D6563666F6F";

            Tree tree = Helper.Read<Tree>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Equal(12, tree.GetAge());
            Assert.Equal("foo", tree.GetName());
        }

        public class Tree2
        {
            [CborProperty("age")]
            private int _age;

            [CborProperty("name")]
            private string _name;

            public int GetAge() => _age;
            public string GetName() => _name;
        }

        [Fact]
        public void TestByAttribute()
        {
            const string hexBuffer = "A2636167650C646E616D6563666F6F";

            Tree2 tree = Helper.Read<Tree2>(hexBuffer);

            Assert.NotNull(tree);
            Assert.Equal(12, tree.GetAge());
            Assert.Equal("foo", tree.GetName());
        }
    }
}
