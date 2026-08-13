using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #142: a document encoded with string references — <c>cbor2.dumps(..., string_referencing=True)</c>,
    /// http://cbor.schmorp.de/stringref — failed with a message about the wrong thing.
    /// </summary>
    /// <remarks>
    /// Stringref is not supported and these tests do not ask for it to be. What they pin is the
    /// diagnosis: the tags are refused by name, at the tag, instead of being skipped and surfacing
    /// later as "Expected major type TextString" at whichever member first repeated a key — an error
    /// that names neither stringref nor the tag that caused it, and points at a part of the document
    /// that is perfectly well formed.
    /// <para>
    /// Refusing is not a narrowing: every one of these documents already threw, since dropping tag 256
    /// and then meeting tag 25 over an index is a type error whatever the reader does with it.
    /// </para>
    /// </remarks>
    public class Issue0142
    {
        public class Measurement
        {
            [CborProperty("temperature")]
            public int Temperature { get; set; }
        }

        /// <summary>
        /// The shape a stringref encoder actually produces: tag 256 around the document, the key
        /// spelled out the first time, and tag 25 over its table index every time after.
        /// </summary>
        /// <remarks>
        /// d9 0100                          tag 256, the namespace
        ///   82                             array(2)
        ///     a1 6b "temperature" 01       the key, which enters the table as index 0
        ///     a1 d8 19 00        02        tag 25 over index 0 — the same key, by reference
        /// </remarks>
        private const string StringRefDocument = "D9010082A16B74656D706572617475726501A1D8190002";

        [Fact]
        public void TheNamespaceTagIsRefusedByName()
        {
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<Measurement[]>(StringRefDocument));

            Assert.Contains("semantic tag 256", ex.Message);
            Assert.Contains("cbor.schmorp.de/stringref", ex.Message);
        }

        /// <summary>
        /// The reference itself, reached without its namespace: a map key is read by a path that does
        /// not skip tags, so this one arrives as a major type mismatch rather than at the tag. It is
        /// named too, which is the case that produced the misleading message in the report.
        /// </summary>
        [Fact]
        public void AStringReferenceUsedAsAKeyIsRefusedByName()
        {
            // a1 d8 19 00 02 -- {stringref(0): 2}
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<Measurement>("A1D8190002"));

            Assert.Contains("semantic tag 25", ex.Message);
            Assert.Contains("cbor.schmorp.de/stringref", ex.Message);
        }

        /// <summary>
        /// Only these two tags are refused. Unrecognised tags are still skipped, which is what lets a
        /// document carrying the self-described CBOR tag read as the value underneath it.
        /// </summary>
        [Fact]
        public void AnUnrelatedTagIsStillSkipped()
        {
            // d9 d9f7 a1 6b "temperature" 01 -- tag 55799 (self-described CBOR) over the map
            Measurement value = Helper.Read<Measurement>("D9D9F7A16B74656D706572617475726501");

            Assert.NotNull(value);
            Assert.Equal(1, value.Temperature);
        }
    }
}
