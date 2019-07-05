using Dahomey.Cbor.Serialization.Conventions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class NamingConventionTests
    {
        [DataTestMethod]
        [DataRow("FooBar", "fooBar")]
        [DataRow("fooBar", "fooBar")]
        [DataRow("Foo", "foo")]
        [DataRow("foo", "foo")]
        [DataRow("", "")]
        public void CamelCase(string srcName, string expectedName)
        {
            ReadOnlyMemory<byte> actualBytes = new CamelCaseNamingConvention().GetPropertyName(srcName);
            Assert.AreEqual(expectedName, Encoding.ASCII.GetString(actualBytes.Span));
        }

        [DataTestMethod]
        [DataRow("FooBar", "foo_bar")]
        [DataRow("fooBar", "foo_bar")]
        [DataRow("FooBAR", "foo_bar")]
        [DataRow("FooBARFoo", "foo_bar_foo")]
        [DataRow("Foo", "foo")]
        [DataRow("foo", "foo")]
        [DataRow("", "")]
        public void SnakeCase(string srcName, string expectedName)
        {
            ReadOnlyMemory<byte> actualBytes = new SnakeCaseNamingConvention().GetPropertyName(srcName);
            Assert.AreEqual(expectedName, Encoding.ASCII.GetString(actualBytes.Span));
        }
    }
}
