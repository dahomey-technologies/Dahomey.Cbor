using System;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public class CddlDateHolder
    {
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
    }

    [CborSerializable(typeof(CddlDateHolder))]
    [CborCddlSchema]
    public partial class CddlDateIso8601Context : CborSerializerContext
    {
    }

    [CborSerializable(typeof(CddlDateHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(DateTimeFormat = DateTimeFormat.Unix)]
    public partial class CddlDateUnixContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(CddlDateHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(DateTimeFormat = DateTimeFormat.UnixMilliseconds)]
    public partial class CddlDateUnixMillisecondsContext : CborSerializerContext
    {
    }

    public class CddlDateOnlyTimeOnlyTests
    {
        private static readonly CddlDateIso8601Context Iso8601Context =
            CborSerializerContext.Default<CddlDateIso8601Context>();

        private static readonly CddlDateUnixContext UnixContext =
            CborSerializerContext.Default<CddlDateUnixContext>();

        private static readonly CddlDateUnixMillisecondsContext UnixMillisecondsContext =
            CborSerializerContext.Default<CddlDateUnixMillisecondsContext>();

        private static readonly CddlDateHolder Value = new CddlDateHolder
        {
            Date = new DateOnly(2026, 8, 17),
            Time = new TimeOnly(1, 2, 3, 841),
        };

        [Fact]
        public void Iso8601RendersRfc8943FullDateAndAnUntaggedTime()
        {
            string schema = CddlDateIso8601Context.CddlSchema;

            Assert.Contains("\"Date\": #6.1004(tstr),", schema);
            Assert.Contains("\"Time\": tstr,", schema);
        }

        [Fact]
        public void UnixRendersRfc8943EpochDayAndAnUntaggedInt()
        {
            string schema = CddlDateUnixContext.CddlSchema;

            Assert.Contains("\"Date\": #6.100(int),", schema);
            Assert.Contains("\"Time\": int,", schema);
        }

        /// <summary>
        /// A date has no time of day to carry milliseconds, so it is the same day count under both
        /// numeric formats -- unlike the time beside it, which does move.
        /// </summary>
        [Fact]
        public void UnixMillisecondsKeepsTheDayCountAndFloatsOnlyTheTime()
        {
            string schema = CddlDateUnixMillisecondsContext.CddlSchema;

            Assert.Contains("\"Date\": #6.100(int),", schema);
            Assert.Contains("\"Time\": float,", schema);
        }

        [CddlFact]
        public void Iso8601RoundTripsAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(Value, Iso8601Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDateIso8601Context.CddlSchema, "CddlDateHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void UnixRoundTripsAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(Value, UnixContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDateUnixContext.CddlSchema, "CddlDateHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void UnixMillisecondsRoundTripsAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(Value, UnixMillisecondsContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDateUnixMillisecondsContext.CddlSchema, "CddlDateHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
