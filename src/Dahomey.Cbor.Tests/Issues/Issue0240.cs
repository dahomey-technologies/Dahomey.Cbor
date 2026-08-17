using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Declared for <see cref="Issue0240.AGeneratedContextWritesWhatTheReflectionPathWrites"/>, and for
    /// the side effect: a context in this assembly enrols every type it declares in
    /// <c>GeneratedCorpusTests</c>, which is what owes <c>HalfHolder</c> a sample there and what keeps
    /// the generated and reflection paths byte-identical as options are added.
    /// </summary>
    [CborSerializable(typeof(Issue0240.HalfHolder))]
    public partial class HalfContext : CborSerializerContext
    {
    }

    /// <summary>
    /// Issue #240: a <see cref="Half"/> was written as its own internal members, because
    /// <c>PrimitiveConverterProvider</c> had no case for it and it fell through to
    /// <c>ObjectConverterProvider</c> like any other struct.
    /// </summary>
    /// <remarks>
    /// Silently, and as a document no other decoder can read — and write-only besides, since all five
    /// of those members are computed or read-only, so reading such a document back yielded
    /// <c>default</c> rather than the value written. Everything needed to encode it properly was
    /// already here: <c>CborWriter.WriteHalf</c>, <c>CborReader.ReadHalf</c> and
    /// <c>CborPrimitive.HalfFloat</c>.
    /// </remarks>
    public class Issue0240
    {
        [Fact]
        public void AHalfIsWrittenAsAHalfFloat()
        {
            // F9 3E00 -- major type 7, additional value 25, binary16 1.5
            Helper.TestWrite((Half)1.5f, "F93E00");
        }

        /// <summary>
        /// The shape the issue reported: as a member, where it was a map of the struct's internals.
        /// </summary>
        [Fact]
        public void AHalfMemberIsANumberRatherThanAnObject()
        {
            // A1 6556616C7565 F93E00 -- {"Value": 1.5}, one member holding a number. The exact bytes
            // are what says it is not a map of the struct's own members, which is what a type with no
            // concrete converter is written as.
            const string hex = "A16556616C7565F93E00";

            Helper.TestWrite(new HalfHolder { Value = (Half)1.5f }, hex);

            Assert.Equal((Half)1.5f, Helper.Read<HalfHolder>(hex).Value);
        }

        /// <summary>
        /// The reader is as tolerant as <c>ReadSingle</c> is, because it is the same reader: a sender
        /// that wrote the value as an integer, as a text string or as a wider float is still read. That
        /// is not a policy this converter chose — it defers to <c>CborReader.ReadHalf</c>.
        /// </summary>
        [Theory]
        [InlineData("F93E00", 1.5)]              // binary16, what the writer emits
        [InlineData("03", 3)]                    // a positive integer
        [InlineData("20", -1)]                   // a negative integer
        [InlineData("63312E35", 1.5)]            // a text string, "1.5"
        [InlineData("FA3FC00000", 1.5)]          // binary32
        [InlineData("FB3FF8000000000000", 1.5)]  // binary64
        public void AHalfIsReadFromWhateverShapeTheSenderChose(string hex, double expected)
        {
            Assert.Equal((Half)expected, Helper.Read<Half>(hex));
        }

        /// <summary>
        /// A value past binary16's range saturates to infinity rather than throwing, which is what the
        /// IEEE conversion does and what a caller declaring <c>Half</c> has asked for.
        /// </summary>
        [Fact]
        public void AValuePastTheRangeSaturates()
        {
            // FA 7F7FFFFF -- binary32 float.MaxValue, ~3.4028235E38, far past binary16's 65504.
            Assert.Equal(Half.PositiveInfinity, Helper.Read<Half>("FA7F7FFFFF"));
        }

        /// <summary>
        /// The one value the writer does not pass through: <c>CborWriter.InternalWriteHalf</c> replaces
        /// any NaN with the canonical quiet one, so a payload-carrying or negative NaN does not reach
        /// the wire. Without that a deterministic encoding would admit 2046 spellings of NaN.
        /// </summary>
        [Theory]
        [InlineData("F97E00")]  // the canonical quiet NaN
        [InlineData("F97E01")]  // a quiet NaN carrying a payload
        [InlineData("F9FE00")]  // a negative quiet NaN
        public void EveryNaNIsWrittenAsTheCanonicalOne(string hex)
        {
            Half read = Helper.Read<Half>(hex);

            Assert.True(Half.IsNaN(read));

            // F9 7E00 -- binary16 quiet NaN, whatever the sender's spelling was.
            Helper.TestWrite(read, "F97E00");
        }

        /// <summary>
        /// The boundary values, so the fixtures are not all one exactly-representable number:
        /// negative zero keeps its sign, the largest finite value is not rounded to infinity, and the
        /// smallest subnormal does not flush to zero.
        /// </summary>
        [Theory]
        [InlineData("F98000")]  // -0.0
        [InlineData("F97BFF")]  // 65504, Half.MaxValue
        [InlineData("F9FBFF")]  // -65504, Half.MinValue
        [InlineData("F90001")]  // 5.9604645E-08, Half.Epsilon, the smallest subnormal
        public void TheEdgesOfBinary16RoundTripByteForByte(string hex)
        {
            Helper.TestWrite(Helper.Read<Half>(hex), hex);
        }

        /// <summary>
        /// The generated path resolves the same converter, so a context writes the bytes the reflection
        /// path writes. Declaring <see cref="HalfContext"/> also enrols <see cref="HalfHolder"/> in
        /// <c>GeneratedCorpusTests</c>, which is what keeps the two in step as options are added.
        /// </summary>
        /// <remarks>
        /// A fresh <c>CborOptions</c> on the reflection side rather than null: null resolves to the
        /// process-wide <c>CborOptions.Default</c>, whose registry state depends on which tests ran
        /// before this one. The RFC 8746 element path is a separate shape and lives in
        /// <c>GeneratedTypedArrayTests</c>, which carries a <c>Half[]</c> member for it.
        /// </remarks>
        [Fact]
        public void AGeneratedContextWritesWhatTheReflectionPathWrites()
        {
            HalfContext context = CborSerializerContext.Default<HalfContext>();
            HalfHolder value = new HalfHolder { Value = (Half)1.5f };

            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value, new CborOptions()), generated, ignoreCase: true);
            Assert.Equal((Half)1.5f, Helper.Read<HalfHolder>(generated).Value);
        }

        public class HalfHolder
        {
            public Half Value { get; set; }
        }
    }
}
