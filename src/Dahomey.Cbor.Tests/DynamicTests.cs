using Dahomey.Cbor.ObjectModel;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class DynamicTests
    {
        [Fact]
        public void ReadObject()
        {
            const string hexBuffer =
                "A666537472696E6763666F6F664E756D626572FB40283D70A3D70A3D64426F6F6CF5644E756C6CF6654172726179820102664F626A656374A162496401";

            dynamic actualObject = Helper.Read<CborObject>(hexBuffer);

            Assert.NotNull(actualObject);
            Assert.Equal("foo", actualObject.String.Value<string>());
            Assert.Equal(12.12, actualObject.Number.Value<double>(), 3);
            Assert.True(actualObject.Bool.Value<bool>());
            Assert.Equal(CborValueType.Null, actualObject.Null.Type);
            Assert.Equal(1, actualObject.Array[0].Value<int>());
            Assert.Equal(2, actualObject.Array[1].Value<int>());
            Assert.Equal(1, actualObject.Object.Id.Value<int>());
        }

        [Fact]
        public void WriteObject()
        {
            const string hexBuffer =
                "A666537472696E6763666F6F664E756D626572FB40283D70A3D70A3D64426F6F6CF5644E756C6CF6654172726179820102664F626A656374A162496401";
            
            dynamic obj = new CborObject();
            obj.String = "foo";
            obj.Number = 12.12;
            obj.Bool = true;
            obj.Null = null;
            obj.Array = new[] { 1, 2 };
            obj.Object = new CborObject();
            obj.Object.Id = 1;

            Helper.TestWrite(obj, hexBuffer);
        }
    }
}
