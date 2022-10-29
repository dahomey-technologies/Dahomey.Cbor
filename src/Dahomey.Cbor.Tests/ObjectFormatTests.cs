using Dahomey.Cbor.Attributes;
using System.Reflection;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class ObjectFormatTests
    {
        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        class Person
        {
            [CborProperty(1)]
            public int Id { get; set; }
            [CborProperty(2)]
            public string Name { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        class Person2
        {
            [CborProperty(0)]
            public int Id { get; set; }
            [CborProperty(1)]
            public string Name { get; set; }
        }

        [Fact]
        public void ReadIntKeyMap()
        {
            const string hexBuffer = "A2010C0263466F6F"; // {1: 12, 2: "Foo"}
            Person person = Helper.Read<Person>(hexBuffer);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [Fact]
        public void WriteIntKeyMap()
        {
            const string hexBuffer = "A2010C0263466F6F"; // {1: 12, 2: "Foo"}
            Person person = new Person
            {
                Id = 12,
                Name = "Foo"
            };

            Helper.TestWrite(person, hexBuffer);
        }

        [Fact]
        public void ReadArray()
        {
            const string hexBuffer = "820C63466F6F"; // [12, "Foo"]
            Person2 person = Helper.Read<Person2>(hexBuffer);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [Fact]
        public void WriteArray()
        {
            const string hexBuffer = "820C63466F6F"; // [12, "Foo"]
            Person2 person = new Person2
            {
                Id = 12,
                Name = "Foo"
            };

            Helper.TestWrite(person, hexBuffer);
        }

        class Person3
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263466F6F")] // {1: 12, 2: "Foo"}
        [InlineData(CborObjectFormat.Array, "820C63466F6F")] // [12, "Foo"]
        public void ReadByApi(CborObjectFormat objectFormat, string hexBuffer) 
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<Person3>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Name)))
                    .SetMemberIndex(index++);
            });

            Person3 person = Helper.Read<Person3>(hexBuffer, options);

            Assert.NotNull(person);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263466F6F")] // {1: 12, 2: "Foo"}
        [InlineData(CborObjectFormat.Array, "820C63466F6F")] // [12, "Foo"]
        public void WriteByApi(CborObjectFormat objectFormat, string hexBuffer)
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<Person3>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(Person3).GetProperty(nameof(Person3.Name)))
                    .SetMemberIndex(index++);
            });

            Person3 person = new Person3
            {
                Id = 12,
                Name = "Foo"
            };

            Helper.TestWrite(person, hexBuffer, null, options);
        }

        private class ObjectWithConstructor
        {
            private int id;
            private string name;

            public int Id => id;
            public string Name => name;
            public int Age { get; set; }

            public ObjectWithConstructor(int id, string name)
            {
                this.id = id;
                this.name = name;
            }
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263666F6F", 12, "foo", 0)] // {1: 12, 2: "foo"}
        [InlineData(CborObjectFormat.IntKeyMap, "A1010C", 12, null, 0)] // {1: 12}
        [InlineData(CborObjectFormat.IntKeyMap, "A3010C0263666F6F030D", 12, "foo", 13)] // {1: 12, 2: "foo", 3: 13}
        [InlineData(CborObjectFormat.Array, "820C63666F6F", 12, "foo", 0)] // [12, "foo"]
        [InlineData(CborObjectFormat.Array, "810C", 12, null, 0)] // [12]
        [InlineData(CborObjectFormat.Array, "830C63666F6F0D", 12, "foo", 13)] // [12, "foo", 13]
        public void ConstructorByApi(CborObjectFormat objectFormat, string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(om =>
            {
                om.AutoMap();
                om.MapCreator(o => new ObjectWithConstructor(o.Id, o.Name));
                om.ClearMemberMappings();
                om.MapMember(typeof(ObjectWithConstructor).GetProperty(nameof(ObjectWithConstructor.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor).GetProperty(nameof(ObjectWithConstructor.Name)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor).GetProperty(nameof(ObjectWithConstructor.Age)))
                    .SetMemberIndex(index++);
            });

            ObjectWithConstructor obj = Helper.Read<ObjectWithConstructor>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }

        private class ObjectWithConstructor2
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }

            [CborConstructor]
            public ObjectWithConstructor2(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        [Theory]
        [InlineData(CborObjectFormat.IntKeyMap, "A2010C0263666F6F", 12, "foo", 0)] // {1: 12, 2: "foo"}
        [InlineData(CborObjectFormat.IntKeyMap, "A1010C", 12, null, 0)] // {1: 12}
        [InlineData(CborObjectFormat.IntKeyMap, "A3010C0263666F6F030D", 12, "foo", 13)] // {1: 12, 2: "foo", 3: 13}
        [InlineData(CborObjectFormat.Array, "820C63666F6F", 12, "foo", 0)] // [12, "foo"]
        [InlineData(CborObjectFormat.Array, "810C", 12, null, 0)] // [12]
        [InlineData(CborObjectFormat.Array, "830C63666F6F0D", 12, "foo", 13)] // [12, "foo", 13]
        public void ConstructorByAttribute(CborObjectFormat objectFormat, string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions
            {
                ObjectFormat = objectFormat
            };

            int index = objectFormat == CborObjectFormat.IntKeyMap ? 1 : 0;

            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor2>(om =>
            {
                om.AutoMap();
                om.ClearMemberMappings();
                om.MapMember(typeof(ObjectWithConstructor2).GetProperty(nameof(ObjectWithConstructor2.Id)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor2).GetProperty(nameof(ObjectWithConstructor2.Name)))
                    .SetMemberIndex(index++);
                om.MapMember(typeof(ObjectWithConstructor2).GetProperty(nameof(ObjectWithConstructor2.Age)))
                    .SetMemberIndex(index++);
            });

            ObjectWithConstructor2 obj = Helper.Read<ObjectWithConstructor2>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }
    }
}
