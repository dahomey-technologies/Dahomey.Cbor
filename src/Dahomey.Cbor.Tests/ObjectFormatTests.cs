using Dahomey.Cbor.Attributes;
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
    }
}
