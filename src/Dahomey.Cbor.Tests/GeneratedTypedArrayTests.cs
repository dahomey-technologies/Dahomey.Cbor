using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// One member per RFC 8746 element type, and no fewer.
    /// </summary>
    /// <remarks>
    /// The ten-type list is duplicated between <c>TypedArrayTags</c> and
    /// <c>TypeCollector.IsTypedArrayElementType</c>, because the generator is an analyzer assembly and
    /// cannot reference the runtime library. This model is what makes the MATCHED PAIR comments at
    /// those two sites true: a type added to or removed from one list alone changes the bytes one path
    /// writes for the corresponding member here, and the byte-identity tests below fail. Leave a member
    /// out and that guarantee silently stops covering its type.
    /// </remarks>
    public class GeneratedTypedArrays
    {
        // Tag 72, sint8.
        public sbyte[] Deltas { get; set; }

        // Tag 69, uint16 little endian.
        public ushort[] Ports { get; set; }

        // Tag 77, sint16 little endian.
        public short[] Counts { get; set; }

        // Tag 70, uint32 little endian.
        public uint[] Checksums { get; set; }

        // Tag 78, sint32 little endian.
        public int[] Offsets { get; set; }

        // Tag 71, uint64 little endian.
        public ulong[] Ticks { get; set; }

        // Tag 79, sint64 little endian.
        public long[] Balances { get; set; }

        /// <summary>
        /// Tag 84, binary16 little endian. The one element type the generator matches by name rather
        /// than by <c>SpecialType</c>, because it has none.
        /// </summary>
        public Half[] Coarse { get; set; }

        // Tag 85, binary32 little endian.
        public float[] Samples { get; set; }

        // Tag 86, binary64 little endian.
        public double[] Precise { get; set; }

        /// <summary>
        /// byte[] is deliberately not a typed array: it stays a plain CBOR byte string, which is both
        /// shorter and what every existing payload contains.
        /// </summary>
        public byte[] Payload { get; set; }
    }

    /// <summary>
    /// A context that writes typed arrays. The mode is a write-time option rather than a registration,
    /// so it is carried on the options the generated <c>Configure</c> registers into.
    /// </summary>
    [CborSerializable(typeof(GeneratedTypedArrays))]
    [CborSerializable(typeof(float[]))]
    public partial class TypedArrayCborContext : CborSerializerContext
    {
        public TypedArrayCborContext()
            : base(new CborOptions { TypedArrayMode = TypedArrayMode.LittleEndian })
        {
        }
    }

    /// <summary>
    /// The generated path must reach <c>TypedArrayConverter&lt;T&gt;</c> by a statically emitted
    /// construction. The reflection path gets there through <c>MakeGenericType</c> +
    /// <c>Activator.CreateInstance</c>, which produces a <c>MissingMethodException</c> under Native AOT
    /// because the closed generic was never compiled.
    /// </summary>
    /// <remarks>
    /// Every assertion here compares generated output against reflection output rather than against a
    /// hex fixture, so the tests cannot drift when the encoding changes.
    /// </remarks>
    public class GeneratedTypedArrayTests
    {
        private static readonly TypedArrayCborContext Context =
            CborSerializerContext.Default<TypedArrayCborContext>();

        private static CborOptions ReflectionOptions() =>
            new CborOptions { TypedArrayMode = TypedArrayMode.LittleEndian };

        // Each array carries a boundary value, so a wrong element size or a byte-order slip changes
        // the payload rather than being absorbed by small positive numbers.
        private static GeneratedTypedArrays Sample() => new GeneratedTypedArrays
        {
            Deltas = new sbyte[] { 0, -1, sbyte.MinValue, sbyte.MaxValue },
            Ports = new ushort[] { 0, 443, ushort.MaxValue },
            Counts = new short[] { 1, -2, 300, short.MinValue },
            Checksums = new uint[] { 0, 1, uint.MaxValue },
            Offsets = new[] { 0, -1, int.MinValue, int.MaxValue },
            Ticks = new ulong[] { 0, ulong.MaxValue },
            Balances = new[] { 0L, -1L, long.MinValue, long.MaxValue },
            Coarse = new[] { (Half)1.5f, (Half)(-2f) },
            Samples = new[] { 1.5f, 2.5f, float.MaxValue },
            Precise = new[] { 1.25, -3.5, double.MaxValue },
            Payload = new byte[] { 1, 2, 3 },
        };

        [Fact]
        public void GeneratedContextWritesTypedArrays()
        {
            GeneratedTypedArrays value = Sample();

            string reflection = Helper.Write(value, ReflectionOptions());
            string generated = Helper.Write(value, Context.Options);

            Assert.Equal(reflection, generated);
        }

        /// <summary>
        /// Guards the byte-identity assertion above from passing vacuously: if neither path emitted a
        /// typed array the two would still agree. Naming every tag also pins the ten-type list itself,
        /// so dropping an element type from both duplicated lists at once — the one desync
        /// byte-identity alone cannot see — still fails.
        /// </summary>
        [Theory]
        [InlineData("D848", "sbyte")]   // tag 72, sint8
        [InlineData("D845", "ushort")]  // tag 69, uint16 little endian
        [InlineData("D84D", "short")]   // tag 77, sint16 little endian
        [InlineData("D846", "uint")]    // tag 70, uint32 little endian
        [InlineData("D84E", "int")]     // tag 78, sint32 little endian
        [InlineData("D847", "ulong")]   // tag 71, uint64 little endian
        [InlineData("D84F", "long")]    // tag 79, sint64 little endian
        [InlineData("D854", "Half")]    // tag 84, binary16 little endian
        [InlineData("D855", "float")]   // tag 85, binary32 little endian
        [InlineData("D856", "double")]  // tag 86, binary64 little endian
        public void GeneratedContextEmitsEveryTypedArrayTag(string tagHex, string elementType)
        {
            string generated = Helper.Write(Sample(), Context.Options);

            Assert.True(
                generated.Contains(tagHex),
                $"The generated context did not tag the {elementType}[] member with {tagHex}. "
                + "Check that the element type is present in both TypedArrayTags and "
                + "TypeCollector.IsTypedArrayElementType.");
        }

        [Fact]
        public void GeneratedContextRoundTripsTypedArrays()
        {
            GeneratedTypedArrays value = Sample();

            string hexBuffer = Helper.Write(value, Context.Options);
            GeneratedTypedArrays actual = Cbor.Deserialize<GeneratedTypedArrays>(
                hexBuffer.HexToBytes(), Context.Options);

            Assert.Equal(value.Deltas, actual.Deltas);
            Assert.Equal(value.Ports, actual.Ports);
            Assert.Equal(value.Counts, actual.Counts);
            Assert.Equal(value.Checksums, actual.Checksums);
            Assert.Equal(value.Offsets, actual.Offsets);
            Assert.Equal(value.Ticks, actual.Ticks);
            Assert.Equal(value.Balances, actual.Balances);
            Assert.Equal(value.Coarse, actual.Coarse);
            Assert.Equal(value.Samples, actual.Samples);
            Assert.Equal(value.Precise, actual.Precise);
            Assert.Equal(value.Payload, actual.Payload);
        }

        /// <summary>
        /// The generated context must read what the reflection path wrote, and the other way round —
        /// a context is a drop-in, not a second dialect.
        /// </summary>
        [Fact]
        public void GeneratedContextReadsWhatTheReflectionPathWrote()
        {
            GeneratedTypedArrays value = Sample();

            byte[] bytes = Helper.Write(value, ReflectionOptions()).HexToBytes();
            GeneratedTypedArrays actual = Cbor.Deserialize<GeneratedTypedArrays>(bytes, Context.Options);

            Assert.Equal(value.Deltas, actual.Deltas);
            Assert.Equal(value.Ports, actual.Ports);
            Assert.Equal(value.Counts, actual.Counts);
            Assert.Equal(value.Checksums, actual.Checksums);
            Assert.Equal(value.Offsets, actual.Offsets);
            Assert.Equal(value.Ticks, actual.Ticks);
            Assert.Equal(value.Balances, actual.Balances);
            Assert.Equal(value.Coarse, actual.Coarse);
            Assert.Equal(value.Samples, actual.Samples);
            Assert.Equal(value.Precise, actual.Precise);
        }

        /// <summary>
        /// byte[] keeps the plain byte string encoding. Promoting it to tag 64 would change every
        /// payload ever written.
        /// </summary>
        [Fact]
        public void ByteArraysStayByteStrings()
        {
            byte[] value = new byte[] { 1, 2, 3 };

            string reflection = Helper.Write(value, ReflectionOptions());
            string generated = Helper.Write(value, Context.Options);

            // 43 010203 -- major type 2, 3-byte byte string: [1, 2, 3].
            // Not 83 010203 (a 3-item array), and not D840 43 010203 (tag 64, uint8 typed array).
            Assert.Equal("43010203", reflection, ignoreCase: true);
            Assert.Equal(reflection, generated);
        }

        /// <summary>
        /// An array declared as a root, not merely reached through a member, gets the same treatment.
        /// </summary>
        [Fact]
        public void TypedArrayRootAccessorIsGenerated()
        {
            Assert.NotNull(Context.ArrayOfSingle);

            float[] value = new[] { 1.5f, 2.5f };

            string reflection = Helper.Write(value, ReflectionOptions());
            string generated = Helper.Write(value, Context.Options);

            Assert.Equal(reflection, generated);
        }

        /// <summary>
        /// With the default mode the generated path must still agree with reflection — the same
        /// converter is registered either way, and it writes an ordinary array.
        /// </summary>
        [Fact]
        public void DefaultModeStillWritesOrdinaryArrays()
        {
            float[] value = new[] { 1.5f, 2.5f };

            string reflection = Helper.Write(value, new CborOptions());
            string generated = Helper.Write(value, DefaultModeContext.Options);

            Assert.Equal(reflection, generated);
            Assert.DoesNotContain("D8", generated, StringComparison.Ordinal);
        }

        private static readonly DefaultModeTypedArrayContext DefaultModeContext =
            CborSerializerContext.Default<DefaultModeTypedArrayContext>();
    }

    [CborSerializable(typeof(float[]))]
    public partial class DefaultModeTypedArrayContext : CborSerializerContext
    {
    }
}
