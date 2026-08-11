using System;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    /// <summary>
    /// A rule closed over the declared members rejects a real, wire-legal <c>[Flags]</c> value:
    /// <c>EnumConverter&lt;T&gt;.WriteInt32</c> writes the raw underlying integer unconditionally, so
    /// <c>Red | Green</c> writes <c>3</c> against a schema that (before this fix) only admitted
    /// <c>0 / 1 / 2 / 4</c>. Covers both write formats, because the two reach the open form by
    /// different routes -- see the remarks on <c>CddlEmitter.EmitEnumRule</c>.
    /// </summary>
    [Flags]
    public enum CddlFlagsColour
    {
        None = 0,
        Red = 1,
        Green = 2,
        Blue = 4,
    }

    public class CddlFlagsHolder
    {
        public CddlFlagsColour Colour { get; set; }
    }

    [CborSerializable(typeof(CddlFlagsHolder))]
    [CborCddlSchema]
    public partial class CddlFlagsContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(CddlFlagsHolder))]
    [CborCddlSchema]
    [CborSourceGenerationOptions(EnumFormat = ValueFormat.WriteToString)]
    public partial class CddlFlagsStringContext : CborSerializerContext
    {
    }

    /// <summary>
    /// A distinct <c>[Flags]</c> enum with a negative declared member (the common "All" sentinel,
    /// <c>~0</c>) -- schema-only coverage for the <c>hasNegative</c> branch in
    /// <c>CddlEmitter.EmitEnumRule</c>, which the round-trip tests above never exercise since
    /// <see cref="CddlFlagsColour"/> has no negative member.
    /// </summary>
    [Flags]
    public enum CddlSignedFlags
    {
        None = 0,
        Some = 1,
        All = -1,
    }

    public class CddlSignedFlagsHolder
    {
        public CddlSignedFlags Value { get; set; }
    }

    [CborSerializable(typeof(CddlSignedFlagsHolder))]
    [CborCddlSchema]
    public partial class CddlSignedFlagsContext : CborSerializerContext
    {
    }

    public class CddlFlagsEnumTests
    {
        private static readonly CddlFlagsContext IntContext =
            CborSerializerContext.Default<CddlFlagsContext>();

        private static readonly CddlFlagsStringContext StringContext =
            CborSerializerContext.Default<CddlFlagsStringContext>();

        [Fact]
        public void DefaultFormatRendersTheOpenIntegerForm()
        {
            // No declared member is negative, so the open form is the unsigned prelude type.
            Assert.Contains("CddlFlagsColour = uint", CddlFlagsContext.CddlSchema);
        }

        [Fact]
        public void WriteToStringFormatRendersNamesPlusTheIntegerFallback()
        {
            Assert.Contains(
                "CddlFlagsColour = \"None\" / \"Red\" / \"Green\" / \"Blue\" / uint",
                CddlFlagsStringContext.CddlSchema);
        }

        [Fact]
        public void FlagsEnumWithANegativeMemberRendersTheSignedIntegerForm()
        {
            Assert.Contains("CddlSignedFlags = int", CddlSignedFlagsContext.CddlSchema);
        }

        [CddlFact]
        public void DefaultFormatCombinationValueValidatesAgainstTheSchema()
        {
            // Red | Green (3) matches no single declared member -- exactly the value the closed
            // form used to reject.
            CddlFlagsHolder value = new CddlFlagsHolder
            {
                Colour = CddlFlagsColour.Red | CddlFlagsColour.Green,
            };

            byte[] cbor = Helper.Write(value, IntContext.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(CddlFlagsContext.CddlSchema, "CddlFlagsHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }

        [CddlFact]
        public void WriteToStringFormatCombinationValueFallsBackToIntegerAndValidates()
        {
            // WriteString has no name for the combination, so it falls back to WriteInt32 -- this
            // is what proves the "/ uint" addition is load-bearing, not just belt-and-braces.
            CddlFlagsHolder value = new CddlFlagsHolder
            {
                Colour = CddlFlagsColour.Red | CddlFlagsColour.Green,
            };

            string hex = Helper.Write(value, StringContext.Options);

            // Sanity on the premise: the combination is the map's only member, so its value is the
            // last byte written. "03" (major type 0, value 3) confirms WriteString really fell back
            // to WriteInt32 rather than, say, coincidentally finding a name.
            Assert.EndsWith("03", hex);

            byte[] cbor = hex.HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlFlagsStringContext.CddlSchema, "CddlFlagsHolder", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
