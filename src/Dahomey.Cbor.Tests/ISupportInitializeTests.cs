using Dahomey.Cbor.Attributes;
using System.ComponentModel;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class ISupportInitializeTests
    {
        public class ObjectWithISupportInitialize : ISupportInitialize
        {
            public int Id { get; set; }
            public string Name { get; set; }

            [CborIgnore]
            public bool BeginInitCalled { get; set; }

            [CborIgnore]
            public bool EndInitCalled { get; set; }

            public void BeginInit()
            {
                Assert.False(BeginInitCalled);
                Assert.False(EndInitCalled);
                Assert.Equal(0, Id);
                Assert.Null(Name);
                BeginInitCalled = true;
            }

            public void EndInit()
            {
                Assert.True(BeginInitCalled);
                Assert.False(EndInitCalled);
                Assert.NotEqual(0, Id);
                Assert.NotNull(Name);
                EndInitCalled = true;
            }
        }

        [Fact]
        public void TestRead()
        {
            const string hexBuffer = "A26249640C644E616D6563666F6F"; // {"Id": 12, "Name": "foo"}
            ObjectWithISupportInitialize obj = Helper.Read<ObjectWithISupportInitialize>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
            Assert.Equal("foo", obj.Name);
            Assert.True(obj.BeginInitCalled);
            Assert.True(obj.EndInitCalled);
        }

        public class ObjectWithISupportInitializeAndConstructor : ISupportInitialize
        {
            public int Id { get; private set; }
            public string Name { get; private set; }

            [CborConstructor]
            public ObjectWithISupportInitializeAndConstructor(int id, string name)
            {
                Id = id;
                Name = name;
            }

            [CborIgnore]
            public bool BeginInitCalled { get; set; }

            [CborIgnore]
            public bool EndInitCalled { get; set; }

            public void BeginInit()
            {
                Assert.False(BeginInitCalled);
                Assert.False(EndInitCalled);
                Assert.NotEqual(0, Id);
                Assert.NotNull(Name);
                BeginInitCalled = true;
            }

            public void EndInit()
            {
                Assert.True(BeginInitCalled);
                Assert.False(EndInitCalled);
                Assert.NotEqual(0, Id);
                Assert.NotNull(Name);
                EndInitCalled = true;
            }
        }

        [Fact]
        public void TestReadWithConstructor()
        {
            const string hexBuffer = "A26249640C644E616D6563666F6F"; // {"Id": 12, "Name": "foo"}
            ObjectWithISupportInitializeAndConstructor obj 
                = Helper.Read<ObjectWithISupportInitializeAndConstructor>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
            Assert.Equal("foo", obj.Name);
            Assert.True(obj.BeginInitCalled);
            Assert.True(obj.EndInitCalled);
        }
    }
}
