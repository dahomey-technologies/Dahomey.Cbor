using System.Numerics;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// The two scalars whose CDDL is neither a prelude name nor a range, and which reach
    /// <c>CddlTypeReference.RenderPrimitive</c> by a route of their own: <c>char</c> is a
    /// <c>SpecialType</c> that renders as text rather than as a number, and <c>BigInteger</c> has no
    /// <c>SpecialType</c> at all and is matched by name.
    /// </summary>
    public class CddlScalars
    {
        public char Initial { get; set; }
        public BigInteger Big { get; set; }
        public CborDecimalFraction Price { get; set; }
        public CborBigFloat Scale { get; set; }
    }

    [CborSerializable(typeof(CddlScalars))]
    [CborCddlSchema]
    public partial class CddlScalarsContext : CborSerializerContext
    {
    }

    public class CddlScalarMappingTests
    {
        private static readonly CddlScalarsContext Context =
            CborSerializerContext.Default<CddlScalarsContext>();

        /// <summary>
        /// <c>CharConverter.Write</c> calls <c>CborWriter.WriteChar</c>, which UTF-8 encodes the one
        /// character and writes it as a text string -- not as an integer code point, which is the
        /// mapping a reader of the type alone would expect.
        /// </summary>
        [Fact]
        public void CharIsATextString()
        {
            Assert.Contains("\"Initial\": tstr,", CddlScalarsContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// All three forms, because <c>CborWriter.WriteBigInteger</c> emits a basic integer whenever
        /// the value fits the ulong-bounded header and only tags beyond it -- so a schema naming just
        /// the bignum tags would reject the common case, and one naming just <c>int</c> would reject
        /// the large one. Parenthesised so the choice is a <c>type2</c>, legal in a memberkey too.
        /// </summary>
        [Fact]
        public void BigIntegerIsTheBasicIntegerOrEitherBignumTag()
        {
            Assert.Contains(
                "\"Big\": (int / #6.2(bstr) / #6.3(bstr)),",
                CddlScalarsContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// Both RFC 8949 §3.4.4 types render as their tag over the two-element array they write, with
        /// the mantissa carrying the same three-way choice <see cref="BigInteger"/> does, and for the
        /// same reason -- it is written by the same method.
        /// </summary>
        [Fact]
        public void ADecimalFractionAndABigFloatRenderAsTheirTags()
        {
            string schema = CddlScalarsContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("\"Price\": #6.4([int, (int / #6.2(bstr) / #6.3(bstr))]),", schema);
            Assert.Contains("\"Scale\": #6.5([int, (int / #6.2(bstr) / #6.3(bstr))]),", schema);
        }

        [CddlFact]
        public void SerializerOutputValidatesAgainstTheSchema()
        {
            CddlScalars value = new CddlScalars
            {
                Initial = 'A',
                Big = new BigInteger(42),
                Price = new CborDecimalFraction(27315, -2),
                // A mantissa past a basic integer, so the instance exercises the bignum arm of the
                // mantissa's choice rather than only the int one.
                Scale = new CborBigFloat(BigInteger.Parse("18446744073709551616"), -1),
            };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlScalarsContext.CddlSchema, "CddlScalars", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// The arm that the basic-integer case never reaches: <c>ulong.MaxValue + 1</c> is the first
        /// value <c>WriteBigInteger</c> cannot fit in a header, so it is written as tag 2 over a byte
        /// string. This is what makes the three-arm choice load-bearing rather than defensive.
        /// </summary>
        [CddlFact]
        public void BignumTaggedOutputValidatesAgainstTheSchema()
        {
            CddlScalars value = new CddlScalars
            {
                Initial = 'A',
                Big = new BigInteger(ulong.MaxValue) + BigInteger.One,
            };

            string hex = Helper.Write(value, Context.Options);

            // Sanity on the premise: c2 is tag 2 (unsigned bignum). Without this the test would still
            // pass if the writer had quietly emitted a basic integer, proving nothing about the tags.
            Assert.Contains("C2", hex);

            CddlResult result = CddlTool.Validate(
                CddlScalarsContext.CddlSchema, "CddlScalars", hex.HexToBytes());

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// A negative magnitude past the header bound takes the tag 3 arm.
        /// </summary>
        [CddlFact]
        public void NegativeBignumTaggedOutputValidatesAgainstTheSchema()
        {
            CddlScalars value = new CddlScalars
            {
                Initial = 'A',
                Big = BigInteger.MinusOne - (new BigInteger(ulong.MaxValue) + BigInteger.One),
            };

            string hex = Helper.Write(value, Context.Options);

            Assert.Contains("C3", hex);

            CddlResult result = CddlTool.Validate(
                CddlScalarsContext.CddlSchema, "CddlScalars", hex.HexToBytes());

            Assert.True(result.Ok, result.Output);
        }
    }
}
