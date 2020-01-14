using Dahomey.Cbor.Attributes;
using Xunit;
using System.Reflection;

namespace Dahomey.Cbor.Tests
{

    public class ClassMemberModifierTests
    {
        public class ObjectWithPrivateProperty
        {
            public int Id { get; set; }

            [CborProperty]
            private int PrivateProp1 { get; set; }

            private int PrivateProp2 { get; set; }

            public ObjectWithPrivateProperty()
            {
            }

            public ObjectWithPrivateProperty(int privateProp1, int privateProp2)
            {
                PrivateProp1 = privateProp1;
                PrivateProp2 = privateProp2;
            }

            public int GetProp1()
            {
                return PrivateProp1;
            }

            public int GetProp2()
            {
                return PrivateProp2;
            }
        }

        [Fact]
        public void TestWritePrivateProperty()
        {
            ObjectWithPrivateProperty obj = new ObjectWithPrivateProperty(2, 3)
            {
                Id = 1,
            };

            const string hexBuffer = "A2624964016C5072697661746550726F703102";

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void TestReadPrivateProperty()
        {
            const string hexBuffer = "A2624964016C5072697661746550726F703102";

            ObjectWithPrivateProperty obj = Helper.Read<ObjectWithPrivateProperty>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(1, obj.Id);
            Assert.Equal(2, obj.GetProp1());
            Assert.Equal(0, obj.GetProp2());
        }

        public class ObjectWithPrivateField
        {
            public int Id;

            [CborProperty]
            private readonly int PrivateProp1;

            private readonly int PrivateProp2;

            public ObjectWithPrivateField()
            {
            }

            public ObjectWithPrivateField(int privateProp1, int privateProp2)
            {
                PrivateProp1 = privateProp1;
                PrivateProp2 = privateProp2;
            }

            public int GetProp1()
            {
                return PrivateProp1;
            }

            public int GetProp2()
            {
                return PrivateProp2;
            }
        }

        [Fact]
        public void TestWritePrivateField()
        {
            ObjectWithPrivateField obj = new ObjectWithPrivateField(2, 3)
            {
                Id = 1,
            };

            const string hexBuffer = "A2624964016C5072697661746550726F703102";

            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void TestReadPrivateField()
        {
            const string hexBuffer = "A2624964016C5072697661746550726F703102";

            ObjectWithPrivateField obj = Helper.Read<ObjectWithPrivateField>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(1, obj.Id);
            Assert.Equal(2, obj.GetProp1());
            Assert.Equal(0, obj.GetProp2());
        }

        public class ObjectWithReadOnlyField
        {
            public readonly int Id;

            public ObjectWithReadOnlyField()
            {
            }

            public ObjectWithReadOnlyField(int id)
            {
                Id = id;
            }
        }

        [Fact]
        public void TestWriteReadOnlyField()
        {
            ObjectWithReadOnlyField obj = new ObjectWithReadOnlyField(1);
            const string hexBuffer = "A162496401";
            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void TestReadReadOnlyField()
        {
            const string hexBuffer = "A162496401";
            ObjectWithReadOnlyField obj = Helper.Read<ObjectWithReadOnlyField>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(0, obj.Id);
        }

        public class ObjectWithConstField
        {
            [CborProperty]
            public const int Id = 1;
        }

        [Fact]
        public void TestWriteConstField()
        {
            ObjectWithConstField obj = new ObjectWithConstField();
            const string hexBuffer = "A162496401";
            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void TestReadConstField()
        {
            const string hexBuffer = "A162496401";
            ObjectWithConstField obj = Helper.Read<ObjectWithConstField>(hexBuffer);

            Assert.NotNull(obj);
        }

        public class ObjectWithStaticField
        {
            [CborProperty]
            public static int Id = 1;
        }

        [Fact]
        public void TestWriteStaticField()
        {
            ObjectWithStaticField obj = new ObjectWithStaticField();
            const string hexBuffer = "A162496401";
            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void TestReadStaticField()
        {
            const string hexBuffer = "A162496401";
            ObjectWithStaticField obj = Helper.Read<ObjectWithStaticField>(hexBuffer);

            Assert.NotNull(obj);
        }

        public class ObjectWithStaticProperty
        {
            [CborProperty]
            public static int Id { get; set; } = 1;
        }

        [Fact]
        public void TestWriteStaticProperty()
        {
            ObjectWithStaticProperty obj = new ObjectWithStaticProperty();
            const string hexBuffer = "A162496401";
            Helper.TestWrite(obj, hexBuffer);
        }

        [Fact]
        public void TestReadStaticProperty()
        {
            const string hexBuffer = "A162496401";
            ObjectWithStaticProperty obj = Helper.Read<ObjectWithStaticProperty>(hexBuffer);

            Assert.NotNull(obj);
        }

        private class Tree
        {
            public const string Id = "Tree.class";
            public readonly string Name = "LemonTree";
            public static int WhatEver = 12;
        }

        [Fact]
        public void TestWriteByApi()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Tree>(objectMapping =>
            {
                objectMapping.AutoMap();
                objectMapping.MapMember(
                    typeof(Tree)
                        .GetField(nameof(Tree.Id), BindingFlags.Public | BindingFlags.Static),
                    typeof(string));
                objectMapping.MapMember(tree => tree.Name);
                objectMapping.MapMember(tree => Tree.WhatEver);
            });

            Tree obj = new Tree();
            const string hexBuffer = "A4644E616D65694C656D6F6E547265656249646A547265652E636C617373644E616D65694C656D6F6E547265656857686174457665720C";
            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
