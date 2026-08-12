using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public class CddlDecimals
    {
        public decimal Amount { get; set; }
        public decimal Tiny { get; set; }
        public decimal Huge { get; set; }
    }

    [CborSerializable(typeof(CddlDecimals))]
    [CborSourceGenerationOptions(DecimalFormat = DecimalFormat.DecimalFraction)]
    [CborCddlSchema]
    public partial class CddlDecimalContext : CborSerializerContext
    {
    }

    /// <summary>
    /// The RFC 8949 §3.4.4 form is the only encoding of a <see cref="decimal"/> a schema can describe,
    /// so <c>DecimalFormat</c> is schema-affecting in the way <c>TypedArrayMode</c> is: without it the
    /// member has no CDDL at all and the generator says so through CBOR1011, which
    /// <c>CddlDiagnosticTests</c> pins from both sides.
    /// </summary>
    public class CddlDecimalFractionTests
    {
        private static readonly CddlDecimalContext Context =
            CborSerializerContext.Default<CddlDecimalContext>();

        /// <summary>
        /// The mantissa is a three-way choice for the reason <c>BigInteger</c>'s is: the writer emits a
        /// basic integer while the value fits the integer header and only tags past it, and a 96-bit
        /// mantissa does go past it. A schema naming only <c>int</c> would reject
        /// <c>decimal.MaxValue</c>, and one naming only the bignum tags would reject every ordinary
        /// amount.
        /// </summary>
        [Fact]
        public void ADecimalIsTagFourOverAnExponentAndAMantissa()
        {
            Assert.Contains(
                "\"Amount\": #6.4([int, (int / #6.2(bstr) / #6.3(bstr))]),",
                CddlDecimalContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// The sample spans what the schema claims: a value whose mantissa fits an integer header, one
        /// scaled to the far end of the type, and one whose mantissa needs the bignum tag.
        /// </summary>
        [CddlFact]
        public void SerializerOutputValidatesAgainstTheSchema()
        {
            CddlDecimals value = new CddlDecimals
            {
                Amount = 273.15m,
                Tiny = 0.0000000000000000000000000001m,
                Huge = decimal.MaxValue,
            };

            byte[] cbor = Helper.Write(value, Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlDecimalContext.CddlSchema, "CddlDecimals", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
