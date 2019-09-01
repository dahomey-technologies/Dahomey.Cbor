using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class ShouldSerializeTests
    {
        private class ObjectWithShouldSerialize
        {
            public int Id { get; set; }

            public bool ShouldSerializeId()
            {
                return Id != 12;
            }
        }

        [Theory]
        [InlineData(12, "A0")] // {}
        [InlineData(13, "A16249640D")] // { "Id": 13}
        public void TestAutomaticMethod(int id, string hexBuffer)
        {
            ObjectWithShouldSerialize obj = new ObjectWithShouldSerialize
            {
                Id = id
            };

            Helper.TestWrite(obj, hexBuffer);
        }

        private class ObjectWithShouldSerialize2
        {
            public int Id { get; set; }
        }

        [Theory]
        [InlineData(12, "A0")] // {}
        [InlineData(13, "A16249640D")] // { "Id": 13}
        public void TestCustomMethod(int id, string hexBuffer)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithShouldSerialize2>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Id)
                        .SetShouldSerializeMethod(o => ((ObjectWithShouldSerialize2)o).Id != 12)
            );

            ObjectWithShouldSerialize2 obj = new ObjectWithShouldSerialize2
            {
                Id = id
            };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
