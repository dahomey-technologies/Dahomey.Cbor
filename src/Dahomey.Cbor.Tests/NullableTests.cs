using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class NullableTests
    {
        public class ObjectWithNullable
        {
            public int Id { get; set; }
            public int? Nullable1 { get; set; }
            public int? Nullable2 { get; set; }
        }

        [Fact]
        public void TestRead()
        {
            CborOptions options = new CborOptions();

            const string hexBuffer = "A36249640C694E756C6C61626C65310D694E756C6C61626C6532F6";
            var obj = Helper.Read<ObjectWithNullable>(hexBuffer, options);

            Assert.NotNull(obj);
            Assert.Equal(12, obj.Id);
            Assert.Equal(13, obj.Nullable1);
            Assert.Null(obj.Nullable2);
        }

        [Fact]
        public void TestWrite()
        {
            CborOptions options = new CborOptions();

            const string hexBuffer = "A36249640C694E756C6C61626C65310D694E756C6C61626C6532F6";
            var obj = new ObjectWithNullable
            {
                Id = 12,
                Nullable1 = 13
            };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
