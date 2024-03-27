using Dahomey.Cbor.Serialization.Conventions;
using Xunit;

namespace Dahomey.Cbor.Tests;

public class NamingConventionTests
{
    [Theory]
    [InlineData("FooBar", "fooBar")]
    [InlineData("fooBar", "fooBar")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    [InlineData("", "")]
    public void CamelCase(string srcName, string expectedName)
    {
        string actualName = new CamelCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "foo_bar")]
    [InlineData("fooBar", "foo_bar")]
    [InlineData("FooBAR", "foo_bar")]
    [InlineData("FooBARFoo", "foo_bar_foo")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    [InlineData("", "")]
    public void SnakeCase(string srcName, string expectedName)
    {
        string actualName = new SnakeCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "FOO_BAR")]
    [InlineData("fooBar", "FOO_BAR")]
    [InlineData("FooBAR", "FOO_BAR")]
    [InlineData("FooBARFoo", "FOO_BAR_FOO")]
    [InlineData("Foo", "FOO")]
    [InlineData("foo", "FOO")]
    [InlineData("", "")]
    public void UpperSnakeCase(string srcName, string expectedName)
    {
        string actualName = new UpperSnakeCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "foo-bar")]
    [InlineData("fooBar", "foo-bar")]
    [InlineData("FooBAR", "foo-bar")]
    [InlineData("FooBARFoo", "foo-bar-foo")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    [InlineData("", "")]
    public void KebabCase(string srcName, string expectedName)
    {
        string actualName = new KebabCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "FOO-BAR")]
    [InlineData("fooBar", "FOO-BAR")]
    [InlineData("FooBAR", "FOO-BAR")]
    [InlineData("FooBARFoo", "FOO-BAR-FOO")]
    [InlineData("Foo", "FOO")]
    [InlineData("foo", "FOO")]
    [InlineData("", "")]
    public void UpperKebabCase(string srcName, string expectedName)
    {
        string actualName = new UpperKebabCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "foobar")]
    [InlineData("fooBar", "foobar")]
    [InlineData("FooBAR", "foobar")]
    [InlineData("FooBARFoo", "foobarfoo")]
    [InlineData("Foo", "foo")]
    [InlineData("foo", "foo")]
    [InlineData("", "")]
    public void LowerCase(string srcName, string expectedName)
    {
        string actualName = new LowerCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }

    [Theory]
    [InlineData("FooBar", "FOOBAR")]
    [InlineData("fooBar", "FOOBAR")]
    [InlineData("FooBAR", "FOOBAR")]
    [InlineData("FooBARFoo", "FOOBARFOO")]
    [InlineData("Foo", "FOO")]
    [InlineData("foo", "FOO")]
    [InlineData("", "")]
    public void UpperCase(string srcName, string expectedName)
    {
        string actualName = new UpperCaseNamingConvention().GetPropertyName(srcName);
        Assert.Equal(expectedName, actualName);
    }
}
