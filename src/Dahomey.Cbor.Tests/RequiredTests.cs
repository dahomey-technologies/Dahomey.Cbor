using Dahomey.Cbor.Attributes;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class RequiredTests
    {
        public class StringObject
        {
            public string String { get; set; }
        }

        [Theory]
        [InlineData(RequirementPolicy.Never, "A0", null)]
        [InlineData(RequirementPolicy.Never, "A166537472696E67F6", null)]
        [InlineData(RequirementPolicy.Never, "A166537472696E6763466F6F", null)]
        [InlineData(RequirementPolicy.Always, "A0", typeof(CborException))]
        [InlineData(RequirementPolicy.Always, "A166537472696E67F6", typeof(CborException))]
        [InlineData(RequirementPolicy.Always, "A166537472696E6763466F6F", null)]
        [InlineData(RequirementPolicy.AllowNull, "A0", typeof(CborException))]
        [InlineData(RequirementPolicy.AllowNull, "A166537472696E67F6", null)]
        [InlineData(RequirementPolicy.AllowNull, "A166537472696E6763466F6F", null)]
        [InlineData(RequirementPolicy.DisallowNull, "A0", null)]
        [InlineData(RequirementPolicy.DisallowNull, "A166537472696E67F6", typeof(CborException))]
        [InlineData(RequirementPolicy.DisallowNull, "A166537472696E6763466F6F", null)]
        public void TestRead(RequirementPolicy requirementPolicy, string hexBuffer, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<StringObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.String).SetRequired(requirementPolicy)
            );

            Helper.TestRead<StringObject>(hexBuffer, expectedExceptionType, options);
        }

        [Theory]
        [InlineData(RequirementPolicy.Never, "", null)]
        [InlineData(RequirementPolicy.Never, null, null)]
        [InlineData(RequirementPolicy.Never, "Foo", null)]
        [InlineData(RequirementPolicy.Always, "", null)]
        [InlineData(RequirementPolicy.Always, null, typeof(CborException))]
        [InlineData(RequirementPolicy.Always, "Foo", null)]
        [InlineData(RequirementPolicy.AllowNull, "", null)]
        [InlineData(RequirementPolicy.AllowNull, null, null)]
        [InlineData(RequirementPolicy.AllowNull, "Foo", null)]
        [InlineData(RequirementPolicy.DisallowNull, "", null)]
        [InlineData(RequirementPolicy.DisallowNull, null, typeof(CborException))]
        [InlineData(RequirementPolicy.DisallowNull, "Foo", null)]
        public void TestWrite(RequirementPolicy requirementPolicy, string value, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<StringObject>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.String).SetRequired(requirementPolicy)
            );

            StringObject obj = new StringObject
            {
                String = value
            };

            Helper.TestWrite(obj, expectedExceptionType, options);
        }

        public class StringObjectWithAttribute
        {
            [CborRequired]
            public string String { get; set; }
        }

        [Theory]
        [InlineData("A0", typeof(CborException))]
        [InlineData("A166537472696E67F6", typeof(CborException))]
        [InlineData("A166537472696E6763466F6F", null)]
        public void TestReadWithAttribute(string hexBuffer, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();
            Helper.TestRead<StringObjectWithAttribute>(hexBuffer, expectedExceptionType, options);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData(null, typeof(CborException))]
        [InlineData("Foo", null)]
        public void TestWriteWithAttribute(string value, Type expectedExceptionType)
        {
            CborOptions options = new CborOptions();

            StringObjectWithAttribute obj = new StringObjectWithAttribute
            {
                String = value
            };

            Helper.TestWrite(obj, expectedExceptionType, options);
        }
    }
}
