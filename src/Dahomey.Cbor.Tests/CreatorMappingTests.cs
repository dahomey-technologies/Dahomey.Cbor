using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CreatorMappingTests
    {
        private class ObjectWithConstructor
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }

            public ObjectWithConstructor(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        [DataTestMethod]
        [DataRow("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [DataRow("A16249640C", 12, null, 0)]
        [DataRow("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void ConstructorByApi(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(o => new ObjectWithConstructor(o.Id, o.Name))
            );

            ObjectWithConstructor obj = Helper.Read<ObjectWithConstructor>(hexBuffer, options);

            Assert.IsNotNull(obj);
            Assert.AreEqual(expectedId, obj.Id);
            Assert.AreEqual(expectedName, obj.Name);
            Assert.AreEqual(expectedAge, obj.Age);
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

        [DataTestMethod]
        [DataRow("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [DataRow("A16249640C", 12, null, 0)]
        [DataRow("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void ConstructorByAttribute(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            ObjectWithConstructor2 obj = Helper.Read<ObjectWithConstructor2>(hexBuffer);

            Assert.IsNotNull(obj);
            Assert.AreEqual(expectedId, obj.Id);
            Assert.AreEqual(expectedName, obj.Name);
            Assert.AreEqual(expectedAge, obj.Age);
        }

        private class Factory
        {
            public ObjectWithConstructor NewObjectWithConstructor(int id, string name)
            {
                return new ObjectWithConstructor(id, name);
            }
        }

        [DataTestMethod]
        [DataRow("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [DataRow("A16249640C", 12, null, 0)]
        [DataRow("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
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

            Assert.IsNotNull(obj);
            Assert.AreEqual(expectedId, obj.Id);
            Assert.AreEqual(expectedName, obj.Name);
            Assert.AreEqual(expectedAge, obj.Age);
        }

        [DataTestMethod]
        [DataRow("A26249640C644E616D6563666F6F", 12, "foo", 0)]
        [DataRow("A16249640C", 12, null, 0)]
        [DataRow("A36249640C644E616D6563666F6F634167650D", 12, "foo", 13)]
        public void FactoryMethodByApi(string hexBuffer, int expectedId, string expectedName, int expectedAge)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithConstructor>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .MapCreator(p => NewObjectWithConstructor(p.Id, p.Name))
            );

            ObjectWithConstructor obj = Helper.Read<ObjectWithConstructor>(hexBuffer, options);

            Assert.IsNotNull(obj);
            Assert.AreEqual(expectedId, obj.Id);
            Assert.AreEqual(expectedName, obj.Name);
            Assert.AreEqual(expectedAge, obj.Age);
        }

        private static ObjectWithConstructor NewObjectWithConstructor(int id, string name)
        {
            return new ObjectWithConstructor(id, name);
        }
    }
}
