using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Numerics;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class GeneratedBigIntegerHolder
    {
        public BigInteger Value { get; set; }
        public BigInteger? Optional { get; set; }
    }

    /// <summary>
    /// Exercises the source-generated path, which is the half of this that a run-time test cannot
    /// reach. If <c>TypeCollector</c> did not know <see cref="BigInteger"/> resolves to a concrete
    /// converter it would classify it unsupported and this file would not compile.
    /// </summary>
    [CborSerializable(typeof(GeneratedBigIntegerHolder))]
    public partial class BigIntegerContext : CborSerializerContext
    {
    }

    /// <summary>
    /// RFC 8949 §3.4.3 bignums. The hex buffers on the tagged cases are the ones spelled out in
    /// appendix A of the RFC.
    /// </summary>
    public class BigIntegerTests
    {
        public class ObjectWithBigInteger
        {
            [CborProperty("v")]
            public BigInteger Value { get; set; }
        }

        [Theory]
        // Basic integers, which a bignum reader has to accept: RFC 8949 makes them the preferred
        // serialization for anything that fits, so this is what most bignum-typed data looks like.
        [InlineData("00", "0")]
        [InlineData("0C", "12")]
        [InlineData("2B", "-12")]
        [InlineData("1BFFFFFFFFFFFFFFFF", "18446744073709551615")]
        [InlineData("3BFFFFFFFFFFFFFFFF", "-18446744073709551616")]
        // Tagged bignums, one step beyond what a basic integer reaches.
        [InlineData("C249010000000000000000", "18446744073709551616")]
        [InlineData("C349010000000000000000", "-18446744073709551617")]
        // A magnitude whose top bit is set, which is where a sign byte goes missing if the
        // little-endian conversion on netstandard2.0 is wrong.
        [InlineData("C243FFFFFF", "16777215")]
        [InlineData("C24180", "128")]
        [InlineData("C34180", "-129")]
        // An empty magnitude is zero, so tag 2 is 0 and tag 3 is -1.
        [InlineData("C240", "0")]
        [InlineData("C340", "-1")]
        // A tagged value small enough for a basic integer is still legal on the wire, even though it
        // is not what this library writes.
        [InlineData("C24101", "1")]
        [InlineData("C34101", "-2")]
        public void Read(string hexBuffer, string expectedValue)
        {
            Helper.TestRead(hexBuffer, BigInteger.Parse(expectedValue));
        }

        [Theory]
        // Under 2^64 the preferred serialization is a basic integer, so a BigInteger member writes the
        // same bytes as the same value typed as a ulong or a long.
        [InlineData("0", "00")]
        [InlineData("12", "0C")]
        [InlineData("-12", "2B")]
        [InlineData("-1", "20")]
        [InlineData("18446744073709551615", "1BFFFFFFFFFFFFFFFF")]
        // -2^64 has a magnitude of ulong.MaxValue: a basic integer, but one WriteInt64 cannot reach.
        [InlineData("-18446744073709551616", "3BFFFFFFFFFFFFFFFF")]
        // Beyond that, the tag.
        [InlineData("18446744073709551616", "C249010000000000000000")]
        [InlineData("-18446744073709551617", "C349010000000000000000")]
        public void Write(string value, string expectedHexBuffer)
        {
            Helper.TestWrite(BigInteger.Parse(value), expectedHexBuffer);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("1")]
        [InlineData("-1")]
        [InlineData("255")]
        [InlineData("-255")]
        [InlineData("18446744073709551615")]
        [InlineData("-18446744073709551616")]
        [InlineData("18446744073709551616")]
        [InlineData("-18446744073709551617")]
        [InlineData("340282366920938463463374607431768211456")]
        [InlineData("-340282366920938463463374607431768211456")]
        public void RoundTrip(string value)
        {
            BigInteger expected = BigInteger.Parse(value);

            Assert.Equal(expected, Helper.Read<BigInteger>(Helper.Write(expected)));
        }

        /// <summary>
        /// A magnitude is written without a leading zero byte. The little-endian form
        /// <see cref="BigInteger.ToByteArray()"/> returns carries one whenever the top bit is set, and
        /// reversing it unstripped would produce a longer, non-preferred encoding of the same number.
        /// </summary>
        [Theory]
        [InlineData("18446744073709551616", "C249010000000000000000")]
        [InlineData("36893488147419103231", "C24901FFFFFFFFFFFFFFFF")]
        [InlineData("340282366920938463463374607431768211455", "C250FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
        public void WriteHasNoLeadingZeroByte(string value, string expectedHexBuffer)
        {
            Helper.TestWrite(BigInteger.Parse(value), expectedHexBuffer);
        }

        /// <summary>
        /// A tag other than 2 or 3 is skipped rather than treated as a bignum, which is how every other
        /// reader on this type behaves.
        /// </summary>
        [Theory]
        [InlineData("C00C", "12")]
        [InlineData("D8640C", "12")]
        public void ReadSkipsForeignTag(string hexBuffer, string expectedValue)
        {
            Helper.TestRead(hexBuffer, BigInteger.Parse(expectedValue));
        }

        [Theory]
        // A text string is rejected rather than parsed - see the remarks on ReadBigInteger.
        [InlineData("63616263")]
        // An untagged byte string is a byte string, not a magnitude.
        [InlineData("43010203")]
        [InlineData("F5")]
        [InlineData("80")]
        [InlineData("A0")]
        // Tag 2 and tag 3 have to be followed by a byte string.
        [InlineData("C20C")]
        [InlineData("C363616263")]
        // One tag, not a chain: a bignum nested under another tag is rejected rather than unwrapped.
        // Every reader on CborReader skips a single tag, so this is the library's existing limit rather
        // than one this type adds - the same limit CborValue.SemanticTag has.
        [InlineData("C1C249010000000000000000")]
        public void ReadInvalid(string hexBuffer)
        {
            Assert.Throws<CborException>(() => Helper.Read<BigInteger>(hexBuffer));
        }

        [Theory]
        [InlineData("A16176C249010000000000000000", "18446744073709551616")]
        [InlineData("A161760C", "12")]
        public void ReadObject(string hexBuffer, string expectedValue)
        {
            ObjectWithBigInteger obj = Helper.Read<ObjectWithBigInteger>(hexBuffer);

            Assert.Equal(BigInteger.Parse(expectedValue), obj.Value);
        }

        [Theory]
        [InlineData("18446744073709551616", "A16176C249010000000000000000")]
        [InlineData("12", "A161760C")]
        public void WriteObject(string value, string expectedHexBuffer)
        {
            ObjectWithBigInteger obj = new ObjectWithBigInteger { Value = BigInteger.Parse(value) };

            Helper.TestWrite(obj, expectedHexBuffer);
        }

        /// <summary>
        /// <see cref="Nullable{T}"/> resolves through NullableConverter to the same converter, so a
        /// nullable member is not a separate registration.
        /// </summary>
        [Theory]
        [InlineData("F6", null)]
        [InlineData("C249010000000000000000", "18446744073709551616")]
        public void ReadNullable(string hexBuffer, string expectedValue)
        {
            BigInteger? value = Helper.Read<BigInteger?>(hexBuffer);

            Assert.Equal(expectedValue == null ? null : BigInteger.Parse(expectedValue), value);
        }

        [Fact]
        public void GeneratedContextRoundTrip()
        {
            BigIntegerContext context = CborSerializerContext.Default<BigIntegerContext>();

            GeneratedBigIntegerHolder holder = new GeneratedBigIntegerHolder
            {
                Value = BigInteger.Parse("18446744073709551616"),
                Optional = BigInteger.Parse("-18446744073709551617"),
            };

            string hexBuffer = Helper.Write(holder, context.Options);

            GeneratedBigIntegerHolder rehydrated = Cbor.Deserialize<GeneratedBigIntegerHolder>(
                hexBuffer.HexToBytes(), context.Options);

            Assert.Equal(holder.Value, rehydrated.Value);
            Assert.Equal(holder.Optional, rehydrated.Optional);
        }
    }
}
