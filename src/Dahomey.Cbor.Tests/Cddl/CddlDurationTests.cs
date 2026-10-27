using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public class CddlDurationHolder
    {
        public TimeSpan Elapsed { get; set; }
    }

    [CborSerializable(typeof(CddlDurationHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(TimeSpanFormat = TimeSpanFormat.Duration)]
    public partial class CddlDurationContext : CborSerializerContext
    {
    }

    public class CddlDurationTests
    {
        private static readonly CddlDurationContext Context =
            CborSerializerContext.Default<CddlDurationContext>();

        [Fact]
        public void DurationIsTagOneThousandTwoOverAMap()
        {
            Assert.Contains(
                "\"Elapsed\": #6.1002({1 => int, ? -9 => uint}),",
                CddlDurationContext.CddlSchema.Replace("\r\n", "\n"));
        }

        /// <summary>
        /// A whole number of seconds, which omits the nanosecond entry the schema marks optional.
        /// </summary>
        [CddlFact]
        public void AWholeSecondValidatesAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(
                new CddlDurationHolder { Elapsed = TimeSpan.FromSeconds(3723) },
                Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDurationContext.CddlSchema, "CddlDurationHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// And one carrying a fraction, which writes it -- so both arms of the optional entry are
        /// checked against the tool rather than only the shorter one.
        /// </summary>
        [CddlFact]
        public void AFractionalValueValidatesAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(
                new CddlDurationHolder { Elapsed = new TimeSpan(0, 1, 2, 3, 500) },
                Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDurationContext.CddlSchema, "CddlDurationHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        /// <summary>
        /// A negative fractional value, whose fraction is the unsigned remainder the schema's uint
        /// admits (RFC 9581 3.3).
        /// </summary>
        [CddlFact]
        public void ANegativeFractionalValueValidatesAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(
                new CddlDurationHolder { Elapsed = TimeSpan.FromSeconds(-1.5) },
                Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDurationContext.CddlSchema, "CddlDurationHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
