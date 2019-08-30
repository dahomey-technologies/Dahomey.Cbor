using Dahomey.Cbor.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class DefaultValueTests
    {
        public class ObjectWithDefaultValue
        {
            [CborIgnoreIfDefault]
            [DefaultValue(12)]
            public int Id { get; set; }

            [CborIgnoreIfDefault]
            [DefaultValue("foo")]
            public string FirstName { get; set; }

            [CborIgnoreIfDefault]
            [DefaultValue("foo")]
            public string LastName { get; set; }

            [CborIgnoreIfDefault]
            [DefaultValue(12)]
            public int Age { get; set; }
        }

        [TestMethod]
        public void WriteByAttribute()
        {
            ObjectWithDefaultValue obj = new ObjectWithDefaultValue
            {
                Id = 13,
                FirstName = "foo",
                LastName = "bar",
                Age = 12
            };

            const string hexBuffer = "A26249640D684C6173744E616D6563626172";

            Helper.TestWrite(obj, hexBuffer);
        }

        public class ObjectWithDefaultValue2
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public int Age { get; set; }
        }

        [TestMethod]
        public void WriteValueByApi()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<ObjectWithDefaultValue2>(objectMapping =>
            {
                objectMapping.AutoMap();
                objectMapping.ClearMemberMappings();
                objectMapping.MapMember(o => o.Id).SetDefaultValue(12).SetIngoreIfDefault(true);
                objectMapping.MapMember(o => o.FirstName).SetDefaultValue("foo").SetIngoreIfDefault(true);
                objectMapping.MapMember(o => o.LastName).SetDefaultValue("foo").SetIngoreIfDefault(true);
                objectMapping.MapMember(o => o.Age).SetDefaultValue(12).SetIngoreIfDefault(true);
            });

            ObjectWithDefaultValue2 obj = new ObjectWithDefaultValue2
            {
                Id = 13,
                FirstName = "foo",
                LastName = "bar",
                Age = 12
            };

            const string hexBuffer = "A26249640D684C6173744E616D6563626172";

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
