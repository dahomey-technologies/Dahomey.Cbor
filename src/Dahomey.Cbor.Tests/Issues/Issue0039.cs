using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0039
    {
        public class ObjectWithProtectedConstructor
        {
            public int Id { get; set; }

            [CborIgnore]
            public bool ProtectedCtor { get; set; }

            protected ObjectWithProtectedConstructor()
            {
                ProtectedCtor = true;
            }

            public ObjectWithProtectedConstructor(int id)
            {
                Id = id;
            }
        }

        [Fact]
        public void TestProtectedConstructor()
        {
            const string hexBuffer = "A16249640C";

            ObjectWithProtectedConstructor obj = Helper.Read<ObjectWithProtectedConstructor>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
            Assert.True(obj.ProtectedCtor);
        }

        public class ObjectWithNonDefaultConstructor
        {
            public int Id { get; set; }

            [CborIgnore]
            public bool NonDefaultId { get; set; }

            public ObjectWithNonDefaultConstructor(int id)
            {
                NonDefaultId = id != default;
                Id = id;
            }
        }

        [Fact]
        public void TestNonDefaultConstructor()
        {
            const string hexBuffer = "A16249640C";

            ObjectWithNonDefaultConstructor obj = Helper.Read<ObjectWithNonDefaultConstructor>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
            Assert.True(obj.NonDefaultId);
        }
    }
}
