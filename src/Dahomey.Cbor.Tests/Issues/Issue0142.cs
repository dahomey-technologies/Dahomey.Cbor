using Dahomey.Cbor.Attributes;
using System.Numerics;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #142: a document encoded with string references — <c>cbor2.dumps(..., string_referencing=True)</c>,
    /// http://cbor.schmorp.de/stringref — failed with a message about the wrong thing, or read a
    /// table index as if it were the value.
    /// </summary>
    /// <remarks>
    /// Stringref is not supported and these tests do not ask for it to be. What they pin is that a
    /// reference is named where it matters and stepped over where it does not: tag 25 is refused, by
    /// name, when the item it stands for is about to be used, while tag 256 and any reference inside a
    /// member nobody reads are skipped like any unrecognised tag. A namespace on its own is ordinary
    /// CBOR, and a discarded member is discarded whether or not its strings could have been resolved,
    /// so refusing either would reject documents that used to read correctly.
    /// </remarks>
    public class Issue0142
    {
        public class Measurement
        {
            [CborProperty("temperature")]
            public int Temperature { get; set; }
        }

        public class DecimalHolder
        {
            [CborProperty("value")]
            public decimal Value { get; set; }
        }

        public class BigIntegerHolder
        {
            [CborProperty("value")]
            public BigInteger Value { get; set; }
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
        public void AStringReferenceIsRefusedByName()
        {
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<Measurement[]>(StringRefDocument));

            Assert.Contains("semantic tag 25", ex.Message);
            Assert.Contains("cbor.schmorp.de/stringref", ex.Message);
        }

        /// <summary>The reference on its own, reached without the namespace around it.</summary>
        [Fact]
        public void AStringReferenceUsedAsAKeyIsRefusedByName()
        {
            // a1 d8 19 00 02 -- {stringref(0): 2}
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<Measurement>("A1D8190002"));

            Assert.Contains("semantic tag 25", ex.Message);
            Assert.Contains("cbor.schmorp.de/stringref", ex.Message);
        }

        /// <summary>
        /// A reference standing where a number belongs is refused too. This is the case that used to
        /// pass silently: the tag was skipped like any other, so the member took the table index as
        /// its value — <c>Temperature = 0</c>, from a document that states no temperature at all.
        /// </summary>
        [Fact]
        public void AStringReferenceUsedAsAValueIsRefusedByName()
        {
            // a1 6b "temperature" d8 19 00 -- {"temperature": stringref(0)}
            CborException ex = Assert.Throws<CborException>(
                () => Helper.Read<Measurement>("A16B74656D7065726174757265D81900"));

            Assert.Contains("semantic tag 25", ex.Message);
        }

        /// <summary>a1 65 "value" d8 19 00 — <c>{"value": stringref(0)}</c>.</summary>
        private const string ReferenceOverAValue = "A16576616C7565D81900";

        /// <summary>
        /// The readers that walk the tag stack themselves — <c>decimal</c>, <c>BigInteger</c> and the
        /// §3.4.4 pair — take their tags through <c>TryReadSemanticTag</c>, which does not refuse, so
        /// each has to refuse the reference itself. Silent otherwise, like the numeric case above:
        /// these are the members that were left taking the table index for their value.
        /// </summary>
        [Fact]
        public void AStringReferenceOverADecimalIsRefusedByName()
        {
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<DecimalHolder>(ReferenceOverAValue));

            Assert.Contains("semantic tag 25", ex.Message);
        }

        [Fact]
        public void AStringReferenceOverABigIntegerIsRefusedByName()
        {
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<BigIntegerHolder>(ReferenceOverAValue));

            Assert.Contains("semantic tag 25", ex.Message);
        }

        /// <summary>The same, for the type that reads a §3.4.4 tag stack of its own.</summary>
        [Fact]
        public void AStringReferenceInPlaceOfADecimalFractionIsRefusedByName()
        {
            // d8 19 00 -- stringref(0) where tag 4 belongs
            CborException ex = Assert.Throws<CborException>(() => Helper.Read<CborDecimalFraction>("D81900"));

            Assert.Contains("semantic tag 25", ex.Message);
        }

        /// <summary>
        /// The namespace tag alone is not refused: <c>string_referencing=True</c> writes it around
        /// every document, including one that repeats nothing, and such a document is ordinary CBOR.
        /// </summary>
        [Fact]
        public void ANamespaceWithoutAReferenceStillReads()
        {
            // d9 0100 a1 6b "temperature" 01
            Measurement value = Helper.Read<Measurement>("D90100A16B74656D706572617475726501");

            Assert.NotNull(value);
            Assert.Equal(1, value.Temperature);
        }

        /// <summary>
        /// A reference inside a member the type does not map is skipped, not refused. Nothing needs
        /// resolving to discard it, and it read correctly before this was diagnosed at all.
        /// </summary>
        [Fact]
        public void AStringReferenceInsideAnUnmappedMemberIsStillSkipped()
        {
            // a2 6b "temperature" 01 61 78 d8 19 00 -- {"temperature": 1, "x": stringref(0)}
            Measurement value = Helper.Read<Measurement>("A26B74656D7065726174757265016178D81900");

            Assert.NotNull(value);
            Assert.Equal(1, value.Temperature);
        }

        /// <summary>
        /// Only tag 25 is refused. Unrecognised tags are still skipped, which is what lets a document
        /// carrying the self-described CBOR tag read as the value underneath it.
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
