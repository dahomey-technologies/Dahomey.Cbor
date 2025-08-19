using System.Linq;
using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests;

public class NamingConventionTests
{
    private class TestObject
    {
        public string FooBar { get; set; }
        public string fooBar { get; set; }
        public string FooBAR { get; set; }
        public string FooBARFoo { get; set; }
        public string Foo { get; set; }
        public string foo { get; set; }
    }
    
    [Theory]
    [InlineData("FooBar", "fooBar")]
    [InlineData("fooBar", "fooBar")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    public void CamelCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new CamelCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "foo_bar")]
    [InlineData("fooBar", "foo_bar")]
    [InlineData("FooBAR", "foo_bar")]
    [InlineData("FooBARFoo", "foo_bar_foo")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    public void SnakeCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new SnakeCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "FOO_BAR")]
    [InlineData("fooBar", "FOO_BAR")]
    [InlineData("FooBAR", "FOO_BAR")]
    [InlineData("FooBARFoo", "FOO_BAR_FOO")]
    [InlineData("Foo", "FOO")]
    [InlineData("foo", "FOO")]
    public void UpperSnakeCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new UpperSnakeCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "foo-bar")]
    [InlineData("fooBar", "foo-bar")]
    [InlineData("FooBAR", "foo-bar")]
    [InlineData("FooBARFoo", "foo-bar-foo")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    public void KebabCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new KebabCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "FOO-BAR")]
    [InlineData("fooBar", "FOO-BAR")]
    [InlineData("FooBAR", "FOO-BAR")]
    [InlineData("FooBARFoo", "FOO-BAR-FOO")]
    [InlineData("Foo", "FOO")]
    [InlineData("foo", "FOO")]
    public void UpperKebabCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new UpperKebabCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "foobar")]
    [InlineData("fooBar", "foobar")]
    [InlineData("FooBAR", "foobar")]
    [InlineData("FooBARFoo", "foobarfoo")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    public void LowerCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new LowerCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "FOOBAR")]
    [InlineData("fooBar", "FOOBAR")]
    [InlineData("FooBAR", "FOOBAR")]
    [InlineData("FooBARFoo", "FOOBARFOO")]
    [InlineData("Foo", "FOO")]
    [InlineData("foo", "FOO")]
    public void UpperCase(string srcName, string expectedName)
    {
        var memberInfo = typeof(TestObject).GetMember(srcName).Single();
        string actualName = new UpperCaseNamingConvention().GetPropertyName(memberInfo);
        Assert.Equal(expectedName, actualName);
    }
}
