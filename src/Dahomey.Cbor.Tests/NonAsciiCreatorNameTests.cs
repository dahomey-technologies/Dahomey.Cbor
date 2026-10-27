using System;
using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A creator argument is matched to its member by name, and the name on the wire is UTF-8. When
    /// the creator's names were derived from the mapping they were encoded as ASCII, so an argument
    /// whose member name has a non-ASCII letter never matched the document's key and arrived as its
    /// default, with no error.
    /// </summary>
    public class NonAsciiCreatorNameTests
    {
        public class Bridge
        {
            public int Größe { get; }
            public string Straße { get; }
            public int Plain { get; set; }

            public Bridge(int größe, string straße)
            {
                Größe = größe;
                Straße = straße;
            }
        }

        public class Attributed
        {
            public int Größe { get; }
            public int Plain { get; set; }

            [CborConstructor]
            public Attributed(int größe)
            {
                Größe = größe;
            }
        }

        // {"Größe": 7, "Straße": "x", "Plain": 9}
        private const string Document =
            "A3" + "674772C3B6C39F65" + "07"
            + "6753747261C39F65" + "6178"
            + "65506C61696E" + "09";

        [Fact]
        public void AnArgumentNamedWithANonAsciiLetterIsMatchedByAMapCreator()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Bridge>(objectMapping =>
                objectMapping.AutoMap().MapCreator(o => new Bridge(o.Größe, o.Straße)));

            Bridge bridge = Cbor.Deserialize<Bridge>(Convert.FromHexString(Document), options);

            Assert.Equal(7, bridge.Größe);
            Assert.Equal("x", bridge.Straße);
            Assert.Equal(9, bridge.Plain);
        }

        [Fact]
        public void AnArgumentNamedWithANonAsciiLetterIsMatchedByAConstructorAttribute()
        {
            // {"Größe": 7, "Plain": 9}
            byte[] document = Convert.FromHexString("A2" + "674772C3B6C39F65" + "07" + "65506C61696E" + "09");

            Attributed attributed = Cbor.Deserialize<Attributed>(document);

            Assert.Equal(7, attributed.Größe);
            Assert.Equal(9, attributed.Plain);
        }
    }
}
