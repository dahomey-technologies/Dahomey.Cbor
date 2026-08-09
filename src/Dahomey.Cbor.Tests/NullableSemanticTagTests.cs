using System;
using System.Numerics;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// How <c>NullableConverter</c> treats a semantic tag, pinned because it is the one place a
    /// nullable read differs from the underlying type's.
    /// </summary>
    public class NullableSemanticTagTests
    {
        /// <summary>
        /// The tag reaches the underlying converter rather than being consumed on the way in. Without
        /// this a converter that dispatches on its own tag is handed the bare content: a bignum
        /// arrived at <c>BigIntegerConverter</c> as a byte string and was rejected.
        /// </summary>
        [Fact]
        public void ANullableSeesItsOwnTag()
        {
            Assert.Equal(
                BigInteger.Parse("18446744073709551616"),
                Helper.Read<BigInteger?>("C249010000000000000000"));
        }

        [Theory]
        [InlineData("F6")]
        // A tagged null is still null, which is what it was before the tag was handed back.
        [InlineData("C1F6")]
        [InlineData("D864F6")]
        public void ANullIsNull(string hexBuffer)
        {
            Assert.Null(Helper.Read<int?>(hexBuffer));
        }

        [Theory]
        [InlineData("0C")]
        [InlineData("C10C")]
        [InlineData("D8640C")]
        public void OneTagIsSkipped(string hexBuffer)
        {
            Assert.Equal(12, Helper.Read<int?>(hexBuffer));
        }

        /// <summary>
        /// On an item that is not null, a nullable reads exactly what its underlying type reads.
        /// </summary>
        /// <remarks>
        /// This is the invariant the change commits to, and it is asserted as one: each case reads the
        /// same bytes as <c>T</c> and as <c>T?</c> and compares the two outcomes, rather than pinning
        /// either to a hard-coded result. A future change that makes the underlying type accept or
        /// reject something new keeps this passing only if the nullable follows.
        /// <para>
        /// Null is excluded because that is the one thing a nullable is supposed to do differently --
        /// <see cref="ANullIsNull"/> covers it.
        /// </para>
        /// <para>
        /// Stacked tags are what broke it. The tag was skipped once here and once in the underlying
        /// converter, so <c>T?</c> was accidentally one tag more lenient than <c>T</c>: <c>C1 C1 0C</c>
        /// read as 12 into an <c>int?</c> and threw into an <c>int</c>.
        /// </para>
        /// </remarks>
        [Theory]
        [InlineData("0C")]
        [InlineData("C10C")]
        [InlineData("C1C10C")]
        [InlineData("C0C10C")]
        [InlineData("C1C1C10C")]
        public void ANullableReadsWhatItsUnderlyingTypeReads(string hexBuffer)
        {
            AssertSameOutcome<int>(hexBuffer);
            AssertSameOutcome<long>(hexBuffer);
            AssertSameOutcome<double>(hexBuffer);
            AssertSameOutcome<decimal>(hexBuffer);
            AssertSameOutcome<bool>(hexBuffer);
            AssertSameOutcome<BigInteger>(hexBuffer);
        }

        /// <summary>
        /// The invariant is behavioural equality, not rejection -- including where the shared outcome
        /// is wrong.
        /// </summary>
        /// <remarks>
        /// <see cref="DateTime"/> is the case that shows the difference: a stacked tag over an RFC 3339
        /// string is not rejected, it is read as a value that is not the one encoded, for
        /// <c>DateTime</c> and <c>DateTime?</c> alike. That is why the tests above compare the two
        /// rather than assert that stacked tags throw.
        /// <para>
        /// The second shape is where handing the tag back costs something real. On <c>C1 C0 74 ...</c>
        /// a <c>DateTime?</c> used to decode correctly, because skipping the outer tag here left the
        /// converter a singly-tagged item it handles; it now meets both tags and produces the same
        /// wrong value the non-nullable always produced. The underlying defect is not in
        /// <see cref="DateTime"/> handling at all -- <c>CborReader.GetCurrentDataItemType</c>
        /// over-consumes a byte on a stacked tag, tracked as
        /// https://github.com/dahomey-technologies/Dahomey.Cbor/issues/183. Nothing here pins the wrong
        /// value, so fixing that will leave these passing, with both sides correct instead of both
        /// sides wrong.
        /// </para>
        /// </remarks>
        [Theory]
        // c0 c0 74 "2020-01-02T03:04:05Z" -- both wrong before and after.
        [InlineData("C0C074323032302D30312D30325430333A30343A30355A")]
        // c1 c0 74 "2020-01-02T03:04:05Z" -- the nullable was right before, and is wrong now.
        [InlineData("C1C074323032302D30312D30325430333A30343A30355A")]
        public void ANullableMatchesItsUnderlyingTypeEvenWhereBothAreWrong(string hexBuffer)
        {
            Assert.Equal(Helper.Read<DateTime>(hexBuffer), Helper.Read<DateTime?>(hexBuffer));
        }

        /// <summary>
        /// Reads <paramref name="hexBuffer"/> as both <typeparamref name="T"/> and
        /// <c>T?</c> and asserts the two agree, treating a thrown <see cref="CborException"/> as an
        /// outcome like any other so that rejection and acceptance are compared the same way.
        /// </summary>
        private static void AssertSameOutcome<T>(string hexBuffer) where T : struct
        {
            Assert.Equal(
                Outcome(() => Helper.Read<T>(hexBuffer)),
                Outcome(() => Helper.Read<T?>(hexBuffer)));
        }

        private static string Outcome<TValue>(Func<TValue> read)
        {
            try
            {
                // Not typeof(TValue): that is int for T and int? for T?, so every comparison would
                // fail. The prefix is here to keep a null apart from a value that renders as nothing.
                TValue value = read();
                return value is null ? "null" : $"value {value}";
            }
            catch (CborException)
            {
                return nameof(CborException);
            }
        }
    }
}
