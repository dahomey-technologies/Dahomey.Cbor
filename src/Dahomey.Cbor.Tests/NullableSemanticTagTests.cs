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
        /// A nullable accepts exactly what its underlying type accepts.
        /// </summary>
        /// <remarks>
        /// Two stacked tags used to read into <c>T?</c> and throw into <c>T</c>: the tag was skipped
        /// once here and once in the underlying converter, so a nullable was accidentally one tag more
        /// lenient. Handing the tag back leaves a single skip. Asserted against the underlying type on
        /// the same bytes rather than against a hard-coded outcome, so the two cannot drift apart
        /// again without this failing.
        /// </remarks>
        [Theory]
        [InlineData("C1C10C")]
        [InlineData("C0C10C")]
        [InlineData("C1C1C10C")]
        public void StackedTagsAreRejectedJustAsTheyAreForTheUnderlyingType(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<int>(hexBuffer));
            Assert.Throws<CborException>(() => Helper.Read<int?>(hexBuffer));
        }

        /// <summary>
        /// The same alignment across the nullable types that reach different converters.
        /// </summary>
        [Theory]
        [InlineData(typeof(long), typeof(long?))]
        [InlineData(typeof(double), typeof(double?))]
        [InlineData(typeof(decimal), typeof(decimal?))]
        [InlineData(typeof(bool), typeof(bool?))]
        public void ANullableRejectsStackedTagsForEveryUnderlyingType(Type underlying, Type nullable)
        {
            Assert.NotNull(underlying);
            Assert.NotNull(nullable);

            Assert.Throws<CborException>(() => Read(underlying));
            Assert.Throws<CborException>(() => Read(nullable));

            static object Read(Type type)
            {
                try
                {
                    return typeof(Helper)
                        .GetMethod(nameof(Helper.Read), new[] { typeof(string), typeof(CborOptions) })!
                        .MakeGenericMethod(type)
                        .Invoke(null, new object?[] { "C1C10C", null });
                }
                catch (System.Reflection.TargetInvocationException exception)
                {
                    throw exception.InnerException!;
                }
            }
        }
    }
}
