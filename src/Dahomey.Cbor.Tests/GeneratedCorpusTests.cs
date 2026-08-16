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

            if (type == typeof(Cddl.Größe))
            {
                return new Cddl.Größe { Value = 3 };
            }

            if (type == typeof(Cddl.Café))
            {
                return new Cddl.Café { Name = "four" };
            }

            if (type == typeof(Cddl.Caf_00E9))
            {
                return new Cddl.Caf_00E9 { Value = 5 };
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
                    // Keys on both sides of the basic-integer boundary, so the comparison covers a
                    // BigInteger reached as a dictionary key rather than only as a member.
                    Keyed = new Dictionary<BigInteger, string>
                    {
                        [12] = "small",
                        [BigInteger.Parse("18446744073709551616")] = "big",
                    },
                };
            }

            // Written as RFC 8949 section 3.4.4 decimal fractions, so the comparison catches a
            // generated context that resolved a DecimalConverter built on default options: it would
            // write the 0xFC form against the reflection path's tag 4. The values span the mantissa
            // widths that form reaches for -- an integer header, and a bignum tag past 64 bits.
            if (type == typeof(GeneratedDecimalHolder))
            {
                return new GeneratedDecimalHolder
                {
                    Value = 273.15m,
                    Optional = decimal.MaxValue,
                    Keyed = new Dictionary<decimal, string>
                    {
                        [1.5m] = "one and a half",
                        [decimal.MinValue] = "the floor",
                    },
                };
            }

            // Both RFC 8949 section 3.4.4 members are past what a basic integer holds on one side and
            // not on the other, so the generated bytes only match the reflection path's if the context
            // resolved the two concrete converters rather than collecting the structs as objects with a
            // Mantissa and an Exponent.
            if (type == typeof(GeneratedFractionHolder))
            {
                return new GeneratedFractionHolder
                {
                    Price = new CborDecimalFraction(BigInteger.Parse("18446744073709551616"), -3),
                    Scale = new CborBigFloat(3, -1),
                    Optional = new CborDecimalFraction(27315, -2),
                };
            }

            // Both encodings of tag 4 in one type. The corpus runs it under two contexts differing only
            // in DecimalFormat, so the decimal member moves between the FC form and tag 4 while the two
            // struct members stay put -- which is the cohabitation, checked against the reflection path
            // rather than against a literal.
            if (type == typeof(GeneratedMixedHolder))
            {
                return new GeneratedMixedHolder
                {
                    Plain = 273.15m,
                    Fraction = new CborDecimalFraction(BigInteger.Parse("18446744073709551616"), -3),
                    Big = new CborBigFloat(3, -1),
                };
            }

            // Tuples at three shapes: within seven, past seven so the converter carries a Rest, and
            // nested twice. The comparison is what catches a generated context that named a different
            // converter or the wrong type arguments -- both would still produce valid CBOR.
            if (type == typeof(GeneratedTupleHolder))
            {
                return GeneratedTupleTests.Sample();
            }

            if (type == typeof(GeneratedRequiredHolder))
            {
                return new GeneratedRequiredHolder { Id = 7, Name = "n" };
            }

            if (type == typeof(GeneratedShouldSerializeHolder))
            {
                return new GeneratedShouldSerializeHolder { Id = 1, Name = "n" };
            }

            if (type == typeof(GeneratedOverrideHolder))
            {
                return new GeneratedOverrideHolder { Id = 7, Name = "seven" };
            }

            if (type == typeof(GeneratedInheritedAttributeHolder))
            {
                return new GeneratedInheritedAttributeHolder
                {
                    Id = 7,
                    Secret = "hidden",
                    Name = "seven",
                };
            }

            if (type == typeof(GeneratedGenericOverrideHolder))
            {
                return new GeneratedGenericOverrideHolder { Value = 3, Label = "three" };
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

            // The CDDL fixtures. They are declared to exercise schema emission, but a context is a
            // context: enrolling them here is what keeps their generated converters pinned to the
            // reflection path's bytes, which no CDDL test asserts -- those compare a schema against a
            // string, and would pass just as well if the emitted converters wrote something else.
            if (type == typeof(Cddl.CddlPerson))
            {
                return new Cddl.CddlPerson
                {
                    Name = "Ada", Age = 36, Rating = 7, Active = true, Score = 1.5,
                };
            }

            if (type == typeof(Cddl.CddlComposite))
            {
                return new Cddl.CddlComposite
                {
                    Colour = Cddl.CddlColour.Green,
                    Optional = 5,
                    Nullable = "text",
                    Tags = new List<string> { "a", "b" },
                    Sizes = new[] { 1, 2, 3 },
                    Counts = new Dictionary<string, int> { ["a"] = 1 },
                    Payload = new byte[] { 1, 2, 3 },
                    Stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                };
            }

            if (type == typeof(Cddl.CddlNullableAnnotations))
            {
                return new Cddl.CddlNullableAnnotations
                {
                    Required = "required",
                    Optional = "optional",
                    Oblivious = "oblivious",
                    RequiredItems = new List<string> { "x" },
                    RequiredValues = new Dictionary<string, string> { ["k"] = "v" },
                };
            }

            if (type == typeof(Cddl.CddlOblivious))
            {
                return new Cddl.CddlOblivious
                {
                    Lookup = new Dictionary<string, string> { ["k"] = "v" },
                    Items = new List<string> { "x" },
                };
            }

            if (type == typeof(Cddl.CddlPacked))
            {
                return new Cddl.CddlPacked { Id = 1, Name = "packed" };
            }

            if (type == typeof(Cddl.CddlRow))
            {
                return new Cddl.CddlRow { Id = 2, Name = "row" };
            }

            if (type == typeof(Cddl.CddlDecimals))
            {
                return new Cddl.CddlDecimals
                {
                    Amount = 273.15m,
                    Tiny = 0.0000000000000000000000000001m,
                    Huge = decimal.MaxValue,
                };
            }

            if (type == typeof(Cddl.CddlEscaped))
            {
                return new Cddl.CddlEscaped { Quoted = 1, Backslash = 2, Newline = 3, Vertical = 4 };
            }

            if (type == typeof(Cddl.CddlEscapedLeaf))
            {
                return new Cddl.CddlEscapedLeaf { Id = 1 };
            }

            if (type == typeof(Cddl.CddlEscapedHolder))
            {
                return new Cddl.CddlEscapedHolder { Item = new Cddl.CddlEscapedLeaf { Id = 2 } };
            }

            // The polymorphic fixtures are sampled through their declared type, not their runtime one:
            // a member typed as the base is what makes the discriminator get written at all.
            if (type == typeof(Cddl.CddlCircle))
            {
                return new Cddl.CddlCircle { Id = 1, Radius = 2.5 };
            }

            if (type == typeof(Cddl.CddlSquare))
            {
                return new Cddl.CddlSquare { Id = 2, Side = 3.5 };
            }

            if (type == typeof(Cddl.CddlDrawing))
            {
                return new Cddl.CddlDrawing
                {
                    Shape = new Cddl.CddlCircle { Id = 1, Radius = 2.5 },
                    KnownCircle = new Cddl.CddlCircle { Id = 3, Radius = 4.5 },
                };
            }

            if (type == typeof(Cddl.CddlArrayBase))
            {
                return new Cddl.CddlArrayBase { Id = 1 };
            }

            if (type == typeof(Cddl.CddlArrayDerived))
            {
                return new Cddl.CddlArrayDerived { Id = 1, Name = "derived" };
            }

            if (type == typeof(Cddl.CddlInputEvent))
            {
                return new Cddl.CddlInputEvent { Id = 1, Device = 2 };
            }

            if (type == typeof(Cddl.CddlClickEvent))
            {
                return new Cddl.CddlClickEvent { Id = 1, Device = 2, X = 3 };
            }

            if (type == typeof(Cddl.CddlEventLog))
            {
                return new Cddl.CddlEventLog
                {
                    Any = new Cddl.CddlInputEvent { Id = 1, Device = 2 },
                    Input = new Cddl.CddlClickEvent { Id = 3, Device = 4, X = 5 },
                };
            }

            if (type == typeof(Cddl.CddlEmailNotification))
            {
                return new Cddl.CddlEmailNotification { Sequence = 1, Address = "a@b.c" };
            }

            if (type == typeof(Cddl.CddlSmsNotification))
            {
                return new Cddl.CddlSmsNotification { Sequence = 2, Number = "+3100" };
            }

            if (type == typeof(Cddl.CddlOutbox))
            {
                return new Cddl.CddlOutbox
                {
                    Pending = new Cddl.CddlEmailNotification { Sequence = 1, Address = "a@b.c" },
                };
            }

            // A single member rather than a combination: these contexts also run under
            // EnumFormat.WriteToString, where a combined [Flags] value writes as one name per bit and
            // the round-trip would be asserting the enum converter's own text handling rather than
            // that the two paths agree. CddlFlagsEnumTests covers the combined case against the schema.
            if (type == typeof(Cddl.CddlFlagsHolder))
            {
                return new Cddl.CddlFlagsHolder { Colour = Cddl.CddlFlagsColour.Red };
            }

            if (type == typeof(Cddl.CddlSignedFlagsHolder))
            {
                return new Cddl.CddlSignedFlagsHolder { Value = Cddl.CddlSignedFlags.Some };
            }

            if (type == typeof(Cddl.CddlEnumRangeHolder))
            {
                return new Cddl.CddlEnumRangeHolder
                {
                    Sparse = Cddl.CddlSparseCode.High,
                    Empty = default,
                    Alias = Cddl.CddlAliasCode.A,
                };
            }

            if (type == typeof(Cddl.CddlEnumStringHolder))
            {
                return new Cddl.CddlEnumStringHolder { Named = Cddl.CddlNamedColour.Green };
            }

            if (type == typeof(Cddl.CddlDateTimeFormatHolder))
            {
                return new Cddl.CddlDateTimeFormatHolder
                {
                    Stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                };
            }

            if (type == typeof(Cddl.CddlTypedArrays))
            {
                return new Cddl.CddlTypedArrays
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
                };
            }

            if (type == typeof(Cddl.CddlRootItem))
            {
                return new Cddl.CddlRootItem { Id = 1 };
            }

            if (type == typeof(List<Cddl.CddlRootItem>))
            {
                return new List<Cddl.CddlRootItem> { new Cddl.CddlRootItem { Id = 1 } };
            }

            if (type == typeof(Cddl.CddlRootItem[]))
            {
                return new[] { new Cddl.CddlRootItem { Id = 2 } };
            }

            if (type == typeof(Dictionary<string, Cddl.CddlRootItem>))
            {
                return new Dictionary<string, Cddl.CddlRootItem>
                {
                    ["a"] = new Cddl.CddlRootItem { Id = 3 },
                };
            }

            if (type == typeof(N.Outer.Inner))
            {
                return new N.Outer.Inner { Value = 1 };
            }

            if (type == typeof(Cddl.CddlRuleNamingOtherHolder))
            {
                return new Cddl.CddlRuleNamingOtherHolder { Value = new N.Other.Inner { Value = 2 } };
            }

            if (type == typeof(Cddl.Left.X))
            {
                return new Cddl.Left.X { Id = 6 };
            }

            if (type == typeof(Cddl.Left.X.poly))
            {
                return new Cddl.Left.X.poly { Value = 7 };
            }

            if (type == typeof(Cddl.Left.XSub))
            {
                return new Cddl.Left.XSub { Id = 8, Extra = 9 };
            }

            if (type == typeof(Cddl.Right.X))
            {
                return new Cddl.Right.X { Id = 10 };
            }

            if (type == typeof(Cddl.Right.poly))
            {
                return new Cddl.Right.poly { Value = 11 };
            }

            if (type == typeof(Cddl.CddlScalars))
            {
                // A BigInteger past the ulong-bounded header, so the corpus covers the tag 2 bignum
                // path rather than only the basic-integer one a small value would take.
                return new Cddl.CddlScalars
                {
                    Initial = 'A',
                    Big = new System.Numerics.BigInteger(ulong.MaxValue) + System.Numerics.BigInteger.One,
                    Pair = (7, "seven"),
                };
            }

            if (type == typeof(Cddl.CddlEmptyFlagsHolder))
            {
                // A memberless enum has no name to take a value from; a cast is the only way.
                return new Cddl.CddlEmptyFlagsHolder { Value = (Cddl.CddlEmptyFlags)3 };
            }

            if (type == typeof(GeneratedOptionHolder))
            {
                return new GeneratedOptionHolder
                {
                    Colour = GeneratedOptionColour.Green,
                    Offsets = new[] { 1, -2 },
                };
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
            // EnumFormat, DateTimeFormat and DecimalFormat are copied for exactly the same reason, and
            // are the three a context can now declare that change the bytes without changing which
            // converter is registered: leaving any of them at its default here compares a generated
            // context writing names, Unix seconds or tag 4 against a reflection path writing ordinals,
            // ISO 8601 or the 0xFC form, which fails for a reason that is this list's omission rather
            // than the generator's doing.
            CborOptions reflection = new CborOptions
            {
                DefaultNamingConvention = generated.DefaultNamingConvention,
                ObjectFormat = generated.ObjectFormat,
                TypedArrayMode = generated.TypedArrayMode,
                Deterministic = generated.Deterministic,
                EnumFormat = generated.EnumFormat,
                DateTimeFormat = generated.DateTimeFormat,
                DecimalFormat = generated.DecimalFormat,
            };

            string reflectionBytes = WriteAs(type, Sample(type), reflection);
            string generatedBytes = WriteAs(type, Sample(type), generated);

            Assert.Equal(reflectionBytes, generatedBytes);
        }

        /// <summary>
        /// Declared types that neither path reads back, so the round-trip theory below cannot cover
        /// them. Named rather than filtered by a predicate, so adding a type here is a deliberate act
        /// with a reason attached and not something a shape can drift into.
        /// </summary>
        /// <remarks>
        /// The entry was reproduced against a plain <c>new CborOptions()</c> with no generated
        /// context in play, so it is a pre-existing library defect rather than anything source
        /// generation does differently. It stays in
        /// <see cref="GeneratedBytesMatchReflectionBytes"/>, which it passes: the two writers agree
        /// byte for byte and it is the shared reader that cannot consume the result.
        /// <list type="bullet">
        /// <item><description>A member declared as an abstract class or an interface. The
        /// discriminator is written -- <c>"_t": "circle"</c> is in the bytes -- but reading such a
        /// member asks for a <c>CreatorMapping</c> instead of resolving it.</description></item>
        /// </list>
        /// <para>
        /// The <c>Array</c> object format entries -- <c>CddlRow</c>, <c>CddlArrayBase</c> and
        /// <c>CddlArrayDerived</c>, whose declared indexes start at 1 rather than 0 -- were removed
        /// once #222 was fixed, and now round-trip here rather than being excluded from the theory.
        /// </para>
        /// </remarks>
        private static readonly HashSet<Type> NotReadBackByEitherPath = new HashSet<Type>
        {
            typeof(Cddl.CddlDrawing),
            typeof(Cddl.CddlEventLog),
            typeof(Cddl.CddlOutbox),
            typeof(Cddl.CddlEscapedHolder),
        };

        public static IEnumerable<object[]> RoundTrippableTypes()
        {
            return DeclaredTypes().Where(row => !NotReadBackByEitherPath.Contains((Type)row[0]));
        }

        /// <summary>
        /// A write-only comparison would pass for a context that cannot read its own output, so each
        /// declared type is read back through the same context and re-written.
        /// </summary>
        [Theory]
        [MemberData(nameof(RoundTrippableTypes))]
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
