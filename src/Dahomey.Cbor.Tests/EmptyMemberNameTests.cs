using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class EmptyMemberName
    {
        [CborProperty("")]
        public int A { get; set; }

        public int B { get; set; }
    }

    [CborSerializable(typeof(EmptyMemberName))]
    public partial class EmptyMemberNameContext : CborSerializerContext
    {
    }

    /// <summary>
    /// <c>[CborProperty("")]</c> names a member with the empty text string. It used to be written as a
    /// <c>null</c> key (<c>F6</c>), and neither that key nor a proper <c>60</c> found the member on
    /// read, so the value was lost (#264).
    /// </summary>
    public class EmptyMemberNameTests
    {
        private const string Expected = "A26005614206";

        [Fact]
        public void TheEmptyNameIsAnEmptyTextString()
        {
            EmptyMemberName value = new EmptyMemberName { A = 5, B = 6 };

            Assert.Equal(Expected, Helper.Write(value));
            Assert.Equal(Expected, Helper.Write(value, new EmptyMemberNameContext().Options));
        }

        [Theory]
        [InlineData("A26005614206")]
        // What earlier versions wrote: the null key still reads into the member.
        [InlineData("A2F605614206")]
        public void TheEmptyNameIsRead(string hexBuffer)
        {
            CborOptions strict = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            foreach (CborOptions options in new[] { strict, new EmptyMemberNameContext().Options })
            {
                EmptyMemberName value = Helper.Read<EmptyMemberName>(hexBuffer, options);
                Assert.Equal(5, value.A);
                Assert.Equal(6, value.B);
            }
        }

        /// <summary>
        /// A key is matched by its whole length: "B\0" is not "B", nor "\0" the empty name.
        /// </summary>
        [Theory]
        [InlineData("A2600562420006")]
        [InlineData("A2610005614206")]
        public void TrailingZeroBytesAreNotIgnored(string hexBuffer)
        {
            CborOptions strict = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };
            Assert.Throws<CborException>(() => Helper.Read<EmptyMemberName>(hexBuffer, strict));
        }
    }
}
