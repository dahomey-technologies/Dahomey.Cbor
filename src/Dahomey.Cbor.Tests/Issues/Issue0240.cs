using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// A <see cref="Half"/> was written as its own internal fields, because
    /// <c>PrimitiveConverterProvider</c> had no case for it and it fell through to
    /// <c>ObjectConverterProvider</c> like any other struct.
    /// </summary>
    /// <remarks>
    /// Silently, and as a document no other decoder can read — reading it back depended on
    /// <c>Half</c>'s private layout. Everything needed to encode it properly was already here:
    /// <c>CborWriter.WriteHalf</c>, <c>CborReader.ReadHalf</c> and <c>CborPrimitive.HalfFloat</c>.
    /// </remarks>
    [CborSerializable(typeof(Issue0240.HalfHolder))]
    public partial class HalfContext : CborSerializerContext
    {
    }

    public class Issue0240
    {
        [Fact]
        public void AHalfIsWrittenAsAHalfFloat()
        {
            // F9 3E00 -- major type 7, additional value 25, binary16 1.5
            Helper.TestWrite((Half)1.5f, "F93E00");
        }

        [Fact]
        public void AHalfRoundTrips()
        {
            Assert.Equal((Half)1.5f, Helper.Read<Half>("F93E00"));
        }

        /// <summary>
        /// The shape the issue reported: as a member, where it was a map of the struct's internals.
        /// </summary>
        [Fact]
        public void AHalfMemberIsANumberRatherThanAnObject()
        {
            string hex = Helper.Write(new HalfHolder { Value = (Half)1.5f });

            // A1 6556616C7565 F93E00 -- {"Value": 1.5}, one member holding a number.
            Assert.Equal("A16556616C7565F93E00", hex, ignoreCase: true);
            Assert.DoesNotContain("4269617365644578706F6E656E74", hex, StringComparison.OrdinalIgnoreCase);

            Assert.Equal((Half)1.5f, Helper.Read<HalfHolder>(hex).Value);
        }

        /// <summary>
        /// The reader is as tolerant as <c>ReadSingle</c> is, because it is the same reader: a sender
        /// that wrote the value as an integer or as a wider float is still read. That is not a policy
        /// this converter chose — it defers to <c>CborReader.ReadHalf</c>.
        /// </summary>
        [Theory]
        [InlineData("03", 3)]                    // a positive integer
        [InlineData("20", -1)]                   // a negative integer
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
            Assert.Equal(Half.PositiveInfinity, Helper.Read<Half>("FA7F7FFFFF"));
        }

        /// <summary>
        /// A generated context over the same shapes, so the corpus holds both paths to the same bytes —
        /// and so the RFC 8746 element path is covered too, which is the one place the library already
        /// referenced <c>Half</c> before this converter existed.
        /// </summary>
        [Fact]
        public void AGeneratedContextWritesWhatTheReflectionPathWrites()
        {
            HalfContext context = new HalfContext();
            HalfHolder value = new HalfHolder { Value = (Half)1.5f };

            Assert.Equal(Helper.Write(value), Helper.Write(value, context.Options), ignoreCase: true);
            Assert.Equal((Half)1.5f, Helper.Read<HalfHolder>(Helper.Write(value, context.Options)).Value);
        }

        public class HalfHolder
        {
            public Half Value { get; set; }
        }
    }
}
