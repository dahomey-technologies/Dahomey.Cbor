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
        /// The invariant is behavioural equality, not rejection.
        /// </summary>
        /// <remarks>
        /// <see cref="DateTime"/> is the case that shows the difference: two stacked tags over an
        /// RFC 3339 string are not rejected, they are read as a value that is not the one encoded --
        /// the same wrong value for <c>DateTime</c> and <c>DateTime?</c> alike. That defect is in
        /// <c>DateTimeConverter</c>, predates this change and is unaffected by it, but it is the reason
        /// the tests above compare the two rather than assert that stacked tags throw. Nothing here
        /// pins the wrong value, so fixing that converter will not fail this.
        /// </remarks>
        [Fact]
        public void ANullableMatchesItsUnderlyingTypeEvenWhereBothAreWrong()
        {
            // c0 c0 74 "2020-01-02T03:04:05Z" -- tag(0) tag(0) over an RFC 3339 string.
            const string hexBuffer = "C0C074323032302D30312D30325430333A30343A30355A";

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
