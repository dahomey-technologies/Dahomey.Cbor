using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// The indefinite-length refusal has to hold wherever the length mode comes from, not only where
    /// it is set on <see cref="CborOptions"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CborLengthModeAttribute"/> on a type or a member outranks
    /// <see cref="CborOptions.ArrayLengthMode"/> and <see cref="CborOptions.MapLengthMode"/>, and a
    /// converter may pass a mode explicitly, so a guard on the options alone is bypassable. The check
    /// belongs at the single point every header passes through.
    /// </remarks>
    public class DeterministicLengthModeTests
    {
        [CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]
        public class IndefiniteByAttribute
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        public class IndefiniteMember
        {
            [CborLengthMode(LengthMode = LengthMode.IndefiniteLength)]
            public List<int> Items { get; set; }
        }

        private static CborOptions Deterministic() => new CborOptions { Deterministic = true };

        [Fact]
        public void AttributeOnATypeCannotProduceAnIndefiniteMap()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(new IndefiniteByAttribute { A = 1, B = 2 }, Deterministic()));

            Assert.Contains("indefinite-length map", exception.Message);
        }

        [Fact]
        public void AttributeOnAMemberCannotProduceAnIndefiniteArray()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(
                    new IndefiniteMember { Items = new List<int> { 1, 2 } }, Deterministic()));

            Assert.Contains("indefinite-length array", exception.Message);
        }

        /// <summary>
        /// Without the flag the attribute keeps working exactly as before.
        /// </summary>
        [Fact]
        public void TheAttributeStillWorksWithoutDeterministic()
        {
            // bf                map(*)
            //    6141 01        "A" 1
            //    6142 02        "B" 2
            //    ff             break
            Helper.TestWrite(new IndefiniteByAttribute { A = 1, B = 2 }, "BF614101614202FF");
        }

        /// <summary>
        /// A definite-length document under the same attribute-free type is unaffected, so the guard
        /// costs nothing on the ordinary path.
        /// </summary>
        [Fact]
        public void DefiniteLengthsAreUntouched()
        {
            Dictionary<string, int> map = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

            // a2 6161 01 6162 02
            Helper.TestWrite(map, "A2616101616202", null, Deterministic());
        }
    }
}
