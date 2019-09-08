using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0025
    {
        public class ObjectWithProtectedConstructor
        {
            public int Id { get; set; }

            protected ObjectWithProtectedConstructor()
            {
            }
        }

        [Fact]
        public void Test()
        {
            const string hexBuffer = "A16249640C";

            ObjectWithProtectedConstructor obj = Helper.Read<ObjectWithProtectedConstructor>(hexBuffer);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
        }
    }
}
