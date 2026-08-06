using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedTypedArrays
    {
        public float[] Samples { get; set; }
        public double[] Precise { get; set; }
        public short[] Counts { get; set; }
        public ulong[] Ticks { get; set; }

        // Half[] is deliberately absent. It is an RFC 8746 element type and the generator's element
        // set names it, but System.Half has no concrete converter in PrimitiveConverterProvider, so a
        // context declaring Half[] is refused by CBOR1002 before typed arrays are reached. That is the
        // designed behaviour -- a loud refusal rather than a silent divergence -- and it is the one of
        // the ten element types the generated path cannot carry.

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
            : base(new CborOptions { TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian })
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
            new CborOptions { TypedArrayMode = TypedArrayMode.ReadWriteLittleEndian };

        private static GeneratedTypedArrays Sample() => new GeneratedTypedArrays
        {
            Samples = new[] { 1.5f, 2.5f, float.MaxValue },
            Precise = new[] { 1.25, -3.5 },
            Counts = new short[] { 1, -2, 300 },
            Ticks = new ulong[] { 0, ulong.MaxValue },
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
        /// typed array the two would still agree.
        /// </summary>
        [Fact]
        public void GeneratedContextEmitsTheTypedArrayTag()
        {
            string generated = Helper.Write(new GeneratedTypedArrays { Samples = new[] { 1.5f } }, Context.Options);

            // D8 55 is tag 85, binary32 little endian.
            Assert.Contains("D855", generated);
        }

        [Fact]
        public void GeneratedContextRoundTripsTypedArrays()
        {
            GeneratedTypedArrays value = Sample();

            string hexBuffer = Helper.Write(value, Context.Options);
            GeneratedTypedArrays actual = Cbor.Deserialize<GeneratedTypedArrays>(
                hexBuffer.HexToBytes(), Context.Options);

            Assert.Equal(value.Samples, actual.Samples);
            Assert.Equal(value.Precise, actual.Precise);
            Assert.Equal(value.Counts, actual.Counts);
            Assert.Equal(value.Ticks, actual.Ticks);
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

            Assert.Equal(value.Samples, actual.Samples);
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
