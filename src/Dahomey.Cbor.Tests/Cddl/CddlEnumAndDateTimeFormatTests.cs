using System;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    // Covers the EmitEnumRule/RenderPrimitive branches CddlCompositeTests never exercises: a
    // non-contiguous value set, a zero-member enum, an aliased value, the WriteToString choice of
    // names, and both non-default DateTimeFormat settings.

    public enum CddlSparseCode
    {
        Low = 0,
        High = 5,
    }

    public enum CddlEmptyEnum
    {
    }

    public enum CddlAliasCode
    {
        A = 1,
        B = 1,
    }

    public class CddlEnumRangeHolder
    {
        public CddlSparseCode Sparse { get; set; }
        public CddlEmptyEnum Empty { get; set; }
        public CddlAliasCode Alias { get; set; }
    }

    [CborSerializable(typeof(CddlEnumRangeHolder))]
    [CborCddlSchema]
    public partial class CddlEnumRangeContext : CborSerializerContext
    {
    }

    public enum CddlNamedColour
    {
        Red = 0,
        Green = 1,
    }

    public class CddlEnumStringHolder
    {
        public CddlNamedColour Named { get; set; }
    }

    [CborSerializable(typeof(CddlEnumStringHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(EnumFormat = ValueFormat.WriteToString)]
    public partial class CddlEnumStringContext : CborSerializerContext
    {
    }

    public class CddlDateTimeFormatHolder
    {
        public DateTime Stamp { get; set; }
    }

    [CborSerializable(typeof(CddlDateTimeFormatHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(DateTimeFormat = DateTimeFormat.Unix)]
    public partial class CddlDateTimeUnixContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(CddlDateTimeFormatHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(DateTimeFormat = DateTimeFormat.UnixMilliseconds)]
    public partial class CddlDateTimeUnixMillisecondsContext : CborSerializerContext
    {
    }

    public class CddlEnumAndDateTimeFormatTests
    {
        private static readonly CddlEnumRangeContext RangeContext =
            CborSerializerContext.Default<CddlEnumRangeContext>();

        private static readonly CddlEnumStringContext StringContext =
            CborSerializerContext.Default<CddlEnumStringContext>();

        private static readonly CddlDateTimeUnixContext UnixContext =
            CborSerializerContext.Default<CddlDateTimeUnixContext>();

        private static readonly CddlDateTimeUnixMillisecondsContext UnixMillisecondsContext =
            CborSerializerContext.Default<CddlDateTimeUnixMillisecondsContext>();

        [Fact]
        public void NonContiguousEnumRendersAChoiceRatherThanARange()
        {
            Assert.Contains("CddlSparseCode = 0 / 5", CddlEnumRangeContext.CddlSchema);
        }

        [Fact]
        public void EmptyEnumFallsBackToTheOpenIntType()
        {
            Assert.Contains("CddlEmptyEnum = int", CddlEnumRangeContext.CddlSchema);
        }

        [Fact]
        public void AliasedEnumValueIsNotDuplicated()
        {
            string schema = CddlEnumRangeContext.CddlSchema;

            Assert.Contains("CddlAliasCode = 1", schema);
            Assert.DoesNotContain("1 / 1", schema);
        }

        [CddlFact]
        public void RangeHolderRoundTripsAgainstTheSchema()
        {
            CddlEnumRangeHolder value = new CddlEnumRangeHolder
            {
                Sparse = CddlSparseCode.High,
                Empty = default,
                Alias = CddlAliasCode.A,
            };

            byte[] cbor = Helper.Write(value, RangeContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlEnumRangeContext.CddlSchema, "CddlEnumRangeHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [Fact]
        public void WriteToStringNonFlagsEnumRendersAChoiceOfQuotedNames()
        {
            Assert.Contains(
                "CddlNamedColour = \"Red\" / \"Green\"", CddlEnumStringContext.CddlSchema);
        }

        [CddlFact]
        public void WriteToStringHolderRoundTripsAgainstTheSchema()
        {
            CddlEnumStringHolder value = new CddlEnumStringHolder { Named = CddlNamedColour.Green };

            byte[] cbor = Helper.Write(value, StringContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlEnumStringContext.CddlSchema, "CddlEnumStringHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [Fact]
        public void UnixDateTimeRendersTaggedInt()
        {
            Assert.Contains("\"Stamp\": #6.1(int),", CddlDateTimeUnixContext.CddlSchema);
        }

        [CddlFact]
        public void UnixDateTimeRoundTripsAgainstTheSchema()
        {
            CddlDateTimeFormatHolder value = new CddlDateTimeFormatHolder
            {
                Stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            };

            byte[] cbor = Helper.Write(value, UnixContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDateTimeUnixContext.CddlSchema, "CddlDateTimeFormatHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [Fact]
        public void UnixMillisecondsDateTimeRendersTaggedFloat()
        {
            Assert.Contains(
                "\"Stamp\": #6.1(float),", CddlDateTimeUnixMillisecondsContext.CddlSchema);
        }

        [CddlFact]
        public void UnixMillisecondsDateTimeRoundTripsAgainstTheSchema()
        {
            CddlDateTimeFormatHolder value = new CddlDateTimeFormatHolder
            {
                Stamp = new DateTime(2020, 1, 1, 0, 0, 0, 123, DateTimeKind.Utc),
            };

            byte[] cbor = Helper.Write(value, UnixMillisecondsContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlDateTimeUnixMillisecondsContext.CddlSchema, "CddlDateTimeFormatHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
