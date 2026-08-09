using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class CorpusBytes
    {
        public byte[] Blob { get; set; }
        public string Name { get; set; }
    }

    public class CorpusNested
    {
        public CorpusBytes Inner { get; set; }
        public List<int> Numbers { get; set; }
    }

    [CborSerializable(typeof(CorpusBytes))]
    [CborSerializable(typeof(CorpusNested))]
    public partial class CorpusContext : CborSerializerContext
    {
    }

    /// <summary>
    /// Walks every type declared by every generated context in the test assembly, asserts the
    /// generated converter writes the same bytes as the reflection path, and reads them back.
    /// </summary>
    /// <remarks>
    /// The enumeration is the point. A hand-written list only protects the types someone remembered
    /// to add, so a type quietly losing its handling — <c>byte[]</c> falling back from
    /// <c>ByteArrayConverter</c> to <c>ArrayConverter&lt;byte&gt;</c>, say — produces valid CBOR that
    /// still round-trips and no failure anywhere. Byte identity with the reflection path is the
    /// contract, and it is only meaningful when it is checked for everything every context declares.
    /// <para>
    /// Discovery is by assembly scan rather than by naming contexts, so a new context is enrolled by
    /// existing. What stays manual is the sample value: <see cref="Sample"/> throws for a type it does
    /// not know, which fails loudly rather than skipping silently, but a newly declared type does need
    /// a line here.
    /// </para>
    /// <para>
    /// This compares encodings, so it cannot reach the shape-level divergences between the two paths:
    /// non-public members, types with no accessible parameterless constructor, and subtypes reached
    /// only through a discriminator. Those need cases of their own — a byte comparison over the types
    /// that happen to be declared is not a substitute for them.
    /// </para>
    /// </remarks>
    /// <summary>
    /// A context asking for RFC 8949 section 4.2.1 deterministic output, enrolled in the corpus so the
    /// generated path is pinned to agree with the reflection path under it.
    /// </summary>
    /// <remarks>
    /// Nothing in the generator knows about determinism: member order is settled by
    /// <c>ObjectConverter</c> and key order by <c>AbstractDictionaryConverter</c> and
    /// <c>CborValueConverter</c>, all of which the generated path shares. This exists to keep that
    /// true rather than to make it true -- a generated context that started emitting its own ordering
    /// would diverge from the reflection path here.
    /// </remarks>
    [CborSerializable(typeof(GeneratedShapes))]
    public partial class DeterministicCborContext : CborSerializerContext
    {
        public DeterministicCborContext()
            : base(new CborOptions { Deterministic = true })
        {
        }
    }

    public class GeneratedCorpusTests
    {
        private static object Sample(Type type)
        {
            if (type == typeof(CorpusBytes))
            {
                return new CorpusBytes { Blob = new byte[] { 1, 2, 3 }, Name = "blob" };
            }

            if (type == typeof(CorpusNested))
            {
                return new CorpusNested
                {
                    Inner = new CorpusBytes { Blob = new byte[] { 4, 5 }, Name = "inner" },
                    Numbers = new List<int> { 1, 2, 3 },
                };
            }

            if (type == typeof(GeneratedPerson))
            {
                return new GeneratedPerson
                {
                    Id = 42,
                    Name = "Ada",
                    Active = true,
                    Score = 99.5,
                    Tags = new List<string> { "math", "cbor" },
                    Address = new GeneratedAddress { City = "London", Number = 7 },
                };
            }

            if (type == typeof(GeneratedShapes))
            {
                return new GeneratedShapes
                {
                    Colour = GeneratedColour.Green,
                    Sizes = new[] { 1, 2, 3 },
                    Optional = 5,
                    Counts = new Dictionary<string, int> { ["a"] = 1 },
                };
            }

            // The typed-array corpus entries. These are what make the corpus bind for RFC 8746: with
            // TypedArrayMode carried onto the reflection side, a generated context that registered an
            // ArrayConverter where a TypedArrayConverter belongs writes a plain array against the
            // reflection path's tag 85, and both tests below fail.
            if (type == typeof(GeneratedTypedArrays))
            {
                return new GeneratedTypedArrays
                {
                    Samples = new[] { 1.5f, 2.5f, float.MaxValue },
                    Precise = new[] { 1.25, -3.5 },
                    Counts = new short[] { 1, -2, 300 },
                    Ticks = new ulong[] { 0, ulong.MaxValue },
                    Payload = new byte[] { 1, 2, 3 },
                };
            }

            // Both members are past what a basic integer holds, so the generated bytes only match the
            // reflection path's if the generated context resolved BigIntegerConverter rather than
            // treating BigInteger as an object with a Sign and an IsEven.
            if (type == typeof(GeneratedBigIntegerHolder))
            {
                return new GeneratedBigIntegerHolder
                {
                    Value = BigInteger.Parse("18446744073709551616"),
                    Optional = BigInteger.Parse("-18446744073709551617"),
                };
            }

            if (type == typeof(float[]))
            {
                return new[] { 1.5f, -2.25f };
            }

            if (type == typeof(ReusedOptionsProbe))
            {
                return new ReusedOptionsProbe { Id = 12 };
            }

            if (type == typeof(MutualA))
            {
                return new MutualA { Id = 1, Peer = new MutualB { Id = 2 } };
            }

            if (type == typeof(MutualB))
            {
                return new MutualB { Id = 2, Peer = new MutualA { Id = 1 } };
            }

            throw new InvalidOperationException(
                $"{type} is declared on a generated context but has no sample; add one to {nameof(Sample)}.");
        }

        /// <summary>
        /// Every <c>[CborSerializable]</c> on every <see cref="CborSerializerContext"/> in the
        /// assembly, so adding a context enrols its types without touching this test.
        /// </summary>
        /// <remarks>
        /// One case per (type, context) pair rather than per type. A type declared on two contexts is
        /// two different configurations of the same converters -- <c>GeneratedShapes</c> is written
        /// both plainly and deterministically -- and collapsing them would test whichever context
        /// <c>Assembly.GetTypes()</c> happened to return first and silently skip the other.
        /// </remarks>
        public static IEnumerable<object[]> DeclaredTypes()
        {
            return typeof(GeneratedCorpusTests).Assembly
                .GetTypes()
                .Where(candidate => typeof(CborSerializerContext).IsAssignableFrom(candidate)
                    && !candidate.IsAbstract)
                .SelectMany(context => context.GetCustomAttributes<CborSerializableAttribute>()
                    .Select(attribute => new object[] { attribute.Type, context }));
        }

        [Theory]
        [MemberData(nameof(DeclaredTypes))]
        public void GeneratedBytesMatchReflectionBytes(Type type, Type contextType)
        {
            CborOptions generated = ContextOptions(contextType);

            // A fresh options object rather than null: null resolves to the process-wide
            // CborOptions.Default, whose registry state depends on which tests ran before this one.
            // The context's own settings are copied onto it rather than restated, so the comparison
            // is generated-versus-reflection under equivalent options and not a second guess at what
            // the context configured.
            // TypedArrayMode and Deterministic are copied for the same reason as the other two, and
            // they are what make this test bind at all: under the default Never a TypedArrayConverter
            // is byte-identical to an ArrayConverter, so a context that forgot to register one would
            // compare equal and pass. With the mode on, the reflection side writes tag 85 and a
            // generated side that fell back to ArrayConverter writes a plain array.
            CborOptions reflection = new CborOptions
            {
                DefaultNamingConvention = generated.DefaultNamingConvention,
                ObjectFormat = generated.ObjectFormat,
                TypedArrayMode = generated.TypedArrayMode,
                Deterministic = generated.Deterministic,
            };

            string reflectionBytes = WriteAs(type, Sample(type), reflection);
            string generatedBytes = WriteAs(type, Sample(type), generated);

            Assert.Equal(reflectionBytes, generatedBytes);
        }

        /// <summary>
        /// A write-only comparison would pass for a context that cannot read its own output, so each
        /// declared type is read back through the same context and re-written.
        /// </summary>
        [Theory]
        [MemberData(nameof(DeclaredTypes))]
        public void GeneratedContextReadsBackWhatItWrote(Type type, Type contextType)
        {
            CborOptions generated = ContextOptions(contextType);

            string written = WriteAs(type, Sample(type), generated);
            object rehydrated = Cbor.Deserialize(type, HexToBytes(written), generated);

            Assert.NotNull(rehydrated);
            Assert.Equal(written, WriteAs(type, rehydrated, generated));
        }

        private static CborOptions ContextOptions(Type contextType)
        {
            return ((CborSerializerContext)Activator.CreateInstance(contextType)).Options;
        }

        /// <summary>
        /// Writes through the non-generic entry point so the declared type drives converter selection,
        /// exactly as it does for a member of that type.
        /// </summary>
        private static string WriteAs(Type type, object value, CborOptions options)
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Cbor.Serialize(value, type, bufferWriter, options);
                return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", string.Empty);
            }
        }

        private static byte[] HexToBytes(string hexBuffer)
        {
            byte[] bytes = new byte[hexBuffer.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexBuffer.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// The regression the corpus exists to catch: <c>byte[]</c> is a CBOR byte string, and a
        /// generated <c>ArrayConverter&lt;byte&gt;</c> would write an array of small integers instead.
        /// Both are valid CBOR and both round-trip, which is why only a byte comparison finds it.
        /// </summary>
        [Fact]
        public void ByteArrayIsWrittenAsAByteString()
        {
            CorpusContext context = CborSerializerContext.Default<CorpusContext>();

            string generated = Helper.Write(
                new CorpusBytes { Blob = new byte[] { 1, 2, 3 }, Name = "blob" }, context.Options);

            // a2                    map(2)
            //    64 426c6f62        "Blob"
            //    43 010203          h'010203'   <- byte string, not 83 01 02 03
            //    64 4e616d65        "Name"
            //    64 626c6f62        "blob"
            Assert.Equal("A264426C6F6243010203644E616D6564626C6F62", generated);
        }
    }
}
