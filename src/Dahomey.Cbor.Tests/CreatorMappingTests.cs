using Dahomey.Cbor.Attributes;
using Xunit;
using System;
using Dahomey.Cbor.Serialization.Conventions;

namespace Dahomey.Cbor.Tests
{
    public class CreatorMappingTests
    {
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
        [InlineData("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [InlineData("A16249640C", 12, null, 0)]
        [InlineData("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void ConstructorByApi(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(o => new ObjectWithConstructor(o.Id, o.Name))
            );

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
        [InlineData("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [InlineData("A16249640C", 12, null, 0)]
        [InlineData("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void ConstructorByAttribute(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            ObjectWithConstructor2 obj = Helper.Read<ObjectWithConstructor2>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }

        private class Factory
        {
            public ObjectWithConstructor NewObjectWithConstructor(int id, string name)
            {
                return new ObjectWithConstructor(id, name);
            }
        }

        [Theory]
        [InlineData("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [InlineData("A16249640C", 12, null, 0)]
        [InlineData("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void FactoryByApi(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            Factory factory = new Factory();
            Func<int, string, ObjectWithConstructor> creatorFunc = (int id, string name) => factory.NewObjectWithConstructor(id, name);

            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(creatorFunc)
            );

            ObjectWithConstructor obj = Helper.Read<ObjectWithConstructor>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }

        [Theory]
        [InlineData("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [InlineData("A16249640C", 12, null, 0)]
        [InlineData("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void FactoryMethodByApi(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(p => NewObjectWithConstructor(p.Id, p.Name))
            );

            ObjectWithConstructor obj = Helper.Read<ObjectWithConstructor>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(expectedId, obj.Id);
            Assert.Equal(expectedName, obj.Name);
            Assert.Equal(expectedAge, obj.Age);
        }

        private static ObjectWithConstructor NewObjectWithConstructor(int id, string name)
        {
            return new ObjectWithConstructor(id, name);
        }

        private interface IFoo
        {
            int Id { get; set; }
        }

        private class Foo : IFoo
        {
            public int Id { get; set; }
        }

        private class ObjectWithInterface
        {
            public IFoo Foo { get; set; }
        }

        [Fact]
        public void Interface()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<IFoo>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(o => new Foo())
            );

            const string hexBuffer = "A163466F6FA16249640C";
            ObjectWithInterface obj = Helper.Read<ObjectWithInterface>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.NotNull(obj.Foo);
            Assert.IsType<Foo>(obj.Foo);
            Assert.Equal(12, obj.Foo.Id);

            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        public abstract class AbstractBar
        {
            public int Id { get; set; }
        }

        public class Bar : AbstractBar
        {
        }

        public class ObjectWithAbstractClass
        {
            public AbstractBar Bar { get; set; }
        }

        [Fact]
        public void AbstractClass()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<AbstractBar>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(o => new Bar())
            );

            const string hexBuffer = "A163426172A16249640C";
            ObjectWithAbstractClass obj = Helper.Read<ObjectWithAbstractClass>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.NotNull(obj.Bar);
            Assert.IsType<Bar>(obj.Bar);
            Assert.Equal(12, obj.Bar.Id);

            Helper.TestRead(hexBuffer, (ObjectWithAbstractClass)null, typeof(CborException));

            Helper.TestWrite(obj, hexBuffer, null, options);
        }

        public abstract class AbstractPerson
        {
            public int Id { get; set; }
        }

        [CborDiscriminator("Person")]
        public class Person : AbstractPerson
        {
            public string Name { get; set; }

            public Person(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        [Fact]
        public void InheritedClassWithConstructor()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType(typeof(Person));

            const string hexBuffer = "A3625F7466506572736F6E6249640C644E616D6563466F6F";
            AbstractPerson obj = Helper.Read<AbstractPerson>(hexBuffer, options);

            Assert.NotNull(obj);
            Person person = Assert.IsType<Person>(obj);
            Assert.Equal(12, person.Id);
            Assert.Equal("Foo", person.Name);
        }

        [CborNamingConvention(typeof(SnakeCaseNamingConvention))]
        private class SnakeCaseGetOnly
        {
            public int FirstValue { get; }
            public string SecondValue { get; }

            [CborConstructor]
            public SnakeCaseGetOnly(int firstValue, string secondValue)
            {
                FirstValue = firstValue;
                SecondValue = secondValue;
            }
        }

        [Fact]
        public void ConstructorWithNamingConvention()
        {
            // {"first_value": 12, "second_value": "foo"}: the parameters are matched on the C# member
            // name, so a naming convention no longer hands them their default value.
            SnakeCaseGetOnly obj = Helper.Read<SnakeCaseGetOnly>("A26B66697273745F76616C75650C6C7365636F6E645F76616C756563666F6F");

            Assert.Equal(12, obj.FirstValue);
            Assert.Equal("foo", obj.SecondValue);
        }

        [CborNamingConvention(typeof(SnakeCaseNamingConvention))]
        private class SnakeCaseInit
        {
            public int FirstValue { get; init; }
            public int SeenByConstructor { get; }

            [CborConstructor]
            public SnakeCaseInit(int firstValue)
            {
                FirstValue = firstValue;
                SeenByConstructor = firstValue;
            }
        }

        [Fact]
        public void ConstructorWithNamingConventionReceivesTheValue()
        {
            // {"first_value": 12}: the value goes through the constructor, not through the init accessor
            // after the constructor has run with the default value.
            SnakeCaseInit obj = Helper.Read<SnakeCaseInit>("A16B66697273745F76616C75650C");

            Assert.Equal(12, obj.FirstValue);
            Assert.Equal(12, obj.SeenByConstructor);
        }

        private class RenamedGetOnly
        {
            [CborProperty("k")]
            public int Value { get; }

            [CborConstructor]
            public RenamedGetOnly(int value)
            {
                Value = value;
            }
        }

        [Fact]
        public void ConstructorWithRenamedProperty()
        {
            // {"k": 12}
            RenamedGetOnly obj = Helper.Read<RenamedGetOnly>("A1616B0C");

            Assert.Equal(12, obj.Value);
        }

        private class ParameterNamedAfterKey
        {
            [CborProperty("key")]
            public int Value { get; }

            [CborConstructor]
            public ParameterNamedAfterKey(int key)
            {
                Value = key;
            }
        }

        [Fact]
        public void ConstructorParameterNamedAfterKey()
        {
            // {"key": 12}: a parameter named after the CBOR key, not the member, still matches.
            ParameterNamedAfterKey obj = Helper.Read<ParameterNamedAfterKey>("A1636B65790C");

            Assert.Equal(12, obj.Value);
        }

        private class ListedNameWithOtherCase
        {
            public int Value { get; }

            [CborConstructor("value")]
            public ListedNameWithOtherCase(int v)
            {
                Value = v;
            }
        }

        [Fact]
        public void ConstructorListedNameWithOtherCase()
        {
            // {"Value": 12}: the listed name matches the member ignoring case, and the value is looked up
            // under the name as it appears in the document.
            ListedNameWithOtherCase obj = Helper.Read<ListedNameWithOtherCase>("A16556616C75650C");

            Assert.Equal(12, obj.Value);
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyGetOnly
        {
            [CborProperty(1)]
            public int FirstValue { get; }

            [CborConstructor]
            public IntKeyGetOnly(int firstValue)
            {
                FirstValue = firstValue;
            }
        }

        [Fact]
        public void ConstructorWithIntKeyMap()
        {
            // {1: 12}
            IntKeyGetOnly obj = Helper.Read<IntKeyGetOnly>("A1010C");

            Assert.Equal(12, obj.FirstValue);
        }
    }
}
