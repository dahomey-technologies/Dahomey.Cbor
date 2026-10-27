// The pinned schema below depends on the array members being NotAnnotated: arrays are reference
// types, so without this directive every member would render "#6.n(bstr) / nil" instead of the bare
// tagged form the InlineData rows expect. See CddlNullableAnnotationTests.cs for the general rule.
#nullable enable annotations
using System;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Cddl
{
    public class CddlTypedArrays
    {
        public sbyte[] Deltas { get; set; }
        public ushort[] Ports { get; set; }
        public short[] Counts { get; set; }
        public uint[] Checksums { get; set; }
        public int[] Offsets { get; set; }
        public ulong[] Ticks { get; set; }
        public long[] Balances { get; set; }
        public float[] Samples { get; set; }
        public double[] Precise { get; set; }

        // The tenth element type. It is the only one matched by name rather than by SpecialType, on
        // both sides of the pair, so it is the row most likely to be missed by an edit to either.
        public Half[] Levels { get; set; }
    }

    [CborSerializable(typeof(CddlTypedArrays))]
    [CborSourceGenerationOptions(TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian)]
    [CborCddlSchema]
    public partial class CddlTypedArrayContext : CborSerializerContext
    {
    }

    public class CddlTypedArrayTests
    {
        private static readonly CddlTypedArrayContext Context =
            CborSerializerContext.Default<CddlTypedArrayContext>();

        private static CddlTypedArrays Sample() => new CddlTypedArrays
        {
            Deltas = new sbyte[] { -1, 2 },
            Ports = new ushort[] { 1, 2 },
            Counts = new short[] { -1, 2 },
            Checksums = new uint[] { 1, 2 },
            Offsets = new[] { -1, 2 },
            Ticks = new ulong[] { 1, 2 },
            Balances = new[] { -1L, 2L },
            Samples = new[] { 1.5f },
            Precise = new[] { 1.25 },
            Levels = new[] { (Half)1.5f },
        };

        /// <summary>
        /// The tag numbers are duplicated: they live in TypedArrayTags in the runtime library and in
        /// the CDDL emitter, which is an analyzer and cannot reference it. Naming every number here as
        /// a literal is what catches a synchronised edit to both copies -- a test that only compared
        /// the two paths against each other would see them agree and pass.
        /// </summary>
        [Theory]
        [InlineData("Deltas", 72)]     // sint8
        [InlineData("Ports", 69)]      // uint16 little endian
        [InlineData("Counts", 77)]     // sint16 little endian
        [InlineData("Checksums", 70)]  // uint32 little endian
        [InlineData("Offsets", 78)]    // sint32 little endian
        [InlineData("Ticks", 71)]      // uint64 little endian
        [InlineData("Balances", 79)]   // sint64 little endian
        [InlineData("Levels", 84)]     // binary16 little endian
        [InlineData("Samples", 85)]    // binary32 little endian
        [InlineData("Precise", 86)]    // binary64 little endian
        public void EveryTypedArrayTagIsEmitted(string member, int tag)
        {
            string expected = $"\"{member}\": #6.{tag}(bstr),";

            Assert.Contains(expected, CddlTypedArrayContext.CddlSchema.Replace("\r\n", "\n"));
        }

        [CddlFact]
        public void SerializerOutputValidatesAgainstTheSchema()
        {
            byte[] cbor = Helper.Write(Sample(), Context.Options).HexToBytes();

            CddlResult result = CddlTool.Validate(
                CddlTypedArrayContext.CddlSchema, "CddlTypedArrays", cbor);

            Assert.True(result.Ok, result.Output);
        }
    }
}
