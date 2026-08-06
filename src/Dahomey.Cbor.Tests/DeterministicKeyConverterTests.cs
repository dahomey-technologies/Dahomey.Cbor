using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Deterministic key order must be the order of the bytes the key converter actually writes.
    /// </summary>
    /// <remarks>
    /// Each case here is one a sort key derived from the CLR type got wrong, because deriving one is
    /// making a prediction about a converter's output that nothing checks.
    /// </remarks>
    public class DeterministicKeyConverterTests
    {
        public enum AliasedKey
        {
            First = 1,
            AlsoFirst = 1,
            Second = 2,
        }

        /// <summary>
        /// Writes a Sku as text, so its order is text order and nothing about it is derivable from
        /// the CLR type.
        /// </summary>
        private class SkuConverter : CborConverterBase<Sku>
        {
            public override Sku Read(ref CborReader reader) => new Sku { Code = reader.ReadString() };

            public override void Write(ref CborWriter writer, Sku value, LengthMode lengthMode)
                => writer.WriteString(value.Code);
        }

        [CborConverter(typeof(SkuConverter))]
        public struct Sku
        {
            public string Code { get; set; }
        }

        private static CborOptions Deterministic() => new CborOptions { Deterministic = true };

        /// <summary>
        /// An enum value with two names is written as the last one, so it must be ordered as the last
        /// one. Ordering it as <c>Enum.GetName</c>'s first name put a nine-byte key ahead of a
        /// six-byte one.
        /// </summary>
        [Fact]
        public void AnAliasedEnumKeyIsOrderedAsTheNameThatGetsWritten()
        {
            CborOptions options = Deterministic();
            options.EnumFormat = ValueFormat.WriteToString;

            Dictionary<AliasedKey, int> value = new Dictionary<AliasedKey, int>
            {
                [AliasedKey.Second] = 2,
                [AliasedKey.First] = 1,
            };

            // "Second" encodes as 66 …, "AlsoFirst" as 69 …, so "Second" sorts first even though its
            // numeric value is larger and its first name ("First") would have sorted before it.
            //
            // A2 map(2)
            //   66 5365636F6E64          "Second"     02
            //   69 416C736F4669727374    "AlsoFirst"  01
            Helper.TestWrite(value, "A2665365636F6E640269416C736F466972737401", null, options);
        }

        /// <summary>
        /// The key's own converter decides both the bytes and the order, for a type no ordering
        /// switch could have had a case for.
        /// </summary>
        [Fact]
        public void ACustomKeyConverterDecidesTheOrder()
        {
            Dictionary<Sku, int> value = new Dictionary<Sku, int>
            {
                [new Sku { Code = "zeta" }] = 20,
                [new Sku { Code = "beta" }] = 100,
            };

            // Equal-length codes, so the comparison reaches the content and the order is the
            // converter's text order rather than anything derivable from the struct.
            //
            // A2 map(2)
            //   64 62657461   "beta"  1864
            //   64 7A657461   "zeta"  14
            Helper.TestWrite(value, "A264626574611864647A65746114", null, Deterministic());
        }

        /// <summary>
        /// A key type with no case in any ordering switch is written, and ordered, by its own
        /// converter rather than rejected.
        /// </summary>
        [Fact]
        public void DateTimeKeysAreSupported()
        {
            Dictionary<DateTime, int> value = new Dictionary<DateTime, int>
            {
                [new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)] = 2,
                [new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)] = 1,
            };

            string hexBuffer = Helper.Write(value, Deterministic());

            // Both keys encode to the same length, so they order bytewise on the ISO 8601 text, which
            // for these two is chronological.
            Assert.Equal(
                "A2C074323032302D30312D30315430303A30303A30305A01"
                + "C074323032312D30312D30315430303A30303A30305A02",
                hexBuffer);
        }
    }
}
