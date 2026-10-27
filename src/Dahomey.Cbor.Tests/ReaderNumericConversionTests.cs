using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// What a <c>float</c>, <c>double</c>, <c>Half</c> or <c>decimal</c> makes of the CBOR item in front
    /// of it: a text string is a number only if the whole string is, a negative integer keeps its sign
    /// however large, and an item the type cannot hold is a <see cref="CborException"/> carrying the
    /// path rather than a BCL exception.
    /// </summary>
    /// <remarks>
    /// Every case runs through the span reader, the sequence reader and the fragmented sequence reader.
    /// </remarks>
    public class ReaderNumericConversionTests
    {
        public class Holder<T>
        {
            public T Value { get; set; }
        }

        private static readonly BigInteger TwoPow64 = BigInteger.Pow(2, 64);

        // ---- plumbing -------------------------------------------------------------------------------

        private static byte[] AsMember(byte[] value)
        {
            // {"Value": <value>}
            return new byte[] { 0xA1, 0x65, (byte)'V', (byte)'a', (byte)'l', (byte)'u', (byte)'e' }
                .Concat(value).ToArray();
        }

        private static byte[] Text(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] header = bytes.Length < 24
                ? new byte[] { (byte)(0x60 + bytes.Length) }
                : new byte[] { 0x78, (byte)bytes.Length };

            return header.Concat(bytes).ToArray();
        }

        private static IEnumerable<Func<byte[], T>> Readers<T>()
        {
            yield return buffer => Cbor.Deserialize<T>(buffer.AsSpan());
            yield return buffer => Cbor.Deserialize<T>(new ReadOnlySequence<byte>(buffer));
            yield return buffer => Cbor.Deserialize<T>(Helper.Fragmentize(buffer));
        }

        private static IEnumerable<T> ReadAll<T>(byte[] buffer)
        {
            return Readers<T>().Select(read => read(buffer)).ToList();
        }

        /// <summary>The exception the three readers all raise, or null if any of them returned a value.</summary>
        private static Exception Refusal<T>(byte[] buffer)
        {
            Exception first = null;

            foreach (Func<byte[], T> read in Readers<T>())
            {
                Exception exception = null;

                try
                {
                    read(buffer);
                }
                catch (Exception e)
                {
                    exception = e;
                }

                if (exception == null)
                {
                    return null;
                }

                first = first ?? exception;
                Assert.Equal(first.GetType(), exception.GetType());
                Assert.Equal(first.Message, exception.Message);
            }

            return first;
        }

        /// <summary>
        /// The item is refused as a <typeparamref name="T"/> - as the document root and as a member,
        /// where the exception names the member - and never accepted by any reader.
        /// </summary>
        private static void AssertRefused<T>(byte[] item)
        {
            Exception root = Refusal<T>(item);
            Assert.True(root != null, $"{typeof(T).Name} accepted {BitConverter.ToString(item)}");
            CborException rootException = Assert.IsType<CborException>(root);
            Assert.Equal("$", rootException.Path);

            Exception member = Refusal<Holder<T>>(AsMember(item));
            Assert.True(member != null, $"Holder<{typeof(T).Name}> accepted {BitConverter.ToString(item)}");
            CborException memberException = Assert.IsType<CborException>(member);
            Assert.Equal("$.Value", memberException.Path);
        }

        // ---- text strings ---------------------------------------------------------------------------

        public static IEnumerable<object[]> NotWhollyANumber()
        {
            foreach (string text in new[]
            {
                "1,5",       // a decimal comma
                "1.5abc",
                "0x10",
                "12 34",
                "1_000",
                "1.5.3",
                "1 ",
                "1.5\0",
                "1e3x",
                "1.5 ",
                "12-",
                "1+1",
                "5 apples",
            })
            {
                yield return new object[] { text };
            }
        }

        [Theory]
        [MemberData(nameof(NotWhollyANumber))]
        public void ATextStringWithTrailingCharactersIsNotANumber(string text)
        {
            byte[] item = Text(text);

            AssertRefused<float>(item);
            AssertRefused<double>(item);
            AssertRefused<Half>(item);
            AssertRefused<decimal>(item);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" 1")]
        [InlineData("abc")]
        [InlineData("e5")]
        [InlineData("-")]
        public void ATextStringThatIsNoNumberAtAllIsStillRefused(string text)
        {
            byte[] item = Text(text);

            AssertRefused<float>(item);
            AssertRefused<double>(item);
            AssertRefused<Half>(item);
            AssertRefused<decimal>(item);
        }

        [Theory]
        [InlineData("1.5", 1.5)]
        [InlineData("1e3", 1000.0)]
        [InlineData("1E+3", 1000.0)]
        [InlineData("1.0e+2", 100.0)]
        [InlineData("+1", 1.0)]
        [InlineData("-1.5", -1.5)]
        [InlineData(".5", 0.5)]
        [InlineData("1.", 1.0)]
        [InlineData("00012", 12.0)]
        [InlineData("0", 0.0)]
        public void ATextStringThatIsWhollyANumberIsStillRead(string text, double expected)
        {
            byte[] item = Text(text);

            Assert.All(ReadAll<double>(item), value => Assert.Equal(expected, value));
            Assert.All(ReadAll<float>(item), value => Assert.Equal((float)expected, value));
            Assert.All(ReadAll<Half>(item), value => Assert.Equal((Half)expected, value));
            Assert.All(ReadAll<decimal>(item), value => Assert.Equal((decimal)expected, value));
        }

        [Fact]
        public void ANegativeZeroTextStringKeepsItsSign()
        {
            byte[] item = Text("-0");

            Assert.All(ReadAll<double>(item), value => Assert.True(double.IsNegative(value) && value == 0));
            Assert.All(ReadAll<float>(item), value => Assert.True(float.IsNegative(value) && value == 0));
            Assert.All(ReadAll<Half>(item), value => Assert.True(Half.IsNegative(value) && value == (Half)0));
            Assert.All(ReadAll<decimal>(item), value => Assert.Equal(0m, value));
        }

        [Theory]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        public void ASpelledOutNonFiniteNumberIsStillReadByTheBinaryFloatingTypesAndRefusedByDecimal(string text)
        {
            byte[] item = Text(text);
            double expected = text == "NaN" ? double.NaN
                : text == "Infinity" ? double.PositiveInfinity : double.NegativeInfinity;

            Assert.All(ReadAll<double>(item), value => Assert.Equal(expected, value));
            Assert.All(ReadAll<float>(item), value => Assert.Equal((float)expected, value));
            Assert.All(ReadAll<Half>(item), value => Assert.Equal((Half)expected, value));
            AssertRefused<decimal>(item);
        }

        // ---- negative integers ----------------------------------------------------------------------

        /// <summary>The argument of a CBOR negative integer, whose value is <c>-1 - argument</c>.</summary>
        public static IEnumerable<object[]> Arguments()
        {
            foreach (BigInteger argument in new BigInteger[]
            {
                0,
                1,
                long.MaxValue,                 // -2^63, the last value a long holds
                (BigInteger)long.MaxValue + 1, // -2^63 - 1, the first it does not
                (BigInteger)long.MaxValue + 2,
                (BigInteger)long.MaxValue + 1 + 1023, // one short of a rounding tie for a double
                (BigInteger)long.MaxValue + 1 + 3071, // the value after it is a tie that rounds up
                (BigInteger)long.MaxValue + 1 + (1L << 40) + (1L << 39) - 1, // the same, for a float
                (BigInteger)ulong.MaxValue - 1,
                (BigInteger)ulong.MaxValue,    // -2^64, the last value the format has
            })
            {
                yield return new object[] { argument.ToString(CultureInfo.InvariantCulture) };
            }
        }

        private static byte[] Item(byte majorType, BigInteger argument)
        {
            byte[] item = new byte[9];
            item[0] = (byte)((majorType << 5) | 27);

            for (int i = 0; i < 8; i++)
            {
                item[8 - i] = (byte)((argument >> (8 * i)) & 0xFF);
            }

            return item;
        }

        /// <summary>
        /// <paramref name="value"/> rounded to a significand of <paramref name="bits"/> bits, ties to
        /// even, as an IEEE 754 conversion rounds it. Done by hand because <c>(double)BigInteger</c>
        /// truncates: 2^63 + 3072 is a tie between 2^63 + 2048 and 2^63 + 4096 that rounds to the
        /// latter, and it answers the former.
        /// </summary>
        private static BigInteger Round(BigInteger magnitude, int bits)
        {
            int excess = (int)magnitude.GetBitLength() - bits;

            if (excess <= 0)
            {
                return magnitude;
            }

            BigInteger quotient = magnitude >> excess;
            BigInteger remainder = magnitude - (quotient << excess);
            BigInteger half = BigInteger.One << (excess - 1);

            if (remainder > half || (remainder == half && !quotient.IsEven))
            {
                quotient += 1;
            }

            return quotient << excess;
        }

        private static double ExactDouble(BigInteger value)
        {
            double magnitude = (double)Round(BigInteger.Abs(value), 53);
            return value.Sign < 0 ? -magnitude : magnitude;
        }

        private static float ExactSingle(BigInteger value)
        {
            float magnitude = (float)(double)Round(BigInteger.Abs(value), 24);
            return value.Sign < 0 ? -magnitude : magnitude;
        }

        /// <summary>A Half holds nothing from 65520 up: those round to infinity, as every narrowing does.</summary>
        private static Half ExactHalf(BigInteger value)
        {
            if (BigInteger.Abs(value) >= 65520)
            {
                return value.Sign < 0 ? Half.NegativeInfinity : Half.PositiveInfinity;
            }

            return (Half)(double)value;
        }

        [Theory]
        [MemberData(nameof(Arguments))]
        public void ANegativeIntegerKeepsItsSignInEveryFloatingType(string argumentText)
        {
            BigInteger argument = BigInteger.Parse(argumentText, CultureInfo.InvariantCulture);
            BigInteger value = BigInteger.MinusOne - argument;
            byte[] item = Item(1, argument);

            // The oracle's own input: the integer types agree on what the item means.
            Assert.All(ReadAll<BigInteger>(item), read => Assert.Equal(value, read));

            Assert.All(ReadAll<double>(item), read => Assert.Equal(ExactDouble(value), read));
            Assert.All(ReadAll<float>(item), read => Assert.Equal(ExactSingle(value), read));
            Assert.All(ReadAll<Half>(item), read => Assert.Equal(ExactHalf(value), read));
            Assert.All(ReadAll<decimal>(item), read => Assert.Equal((decimal)value, read));
        }

        [Theory]
        [MemberData(nameof(Arguments))]
        public void APositiveIntegerOfTheSameMagnitudeIsTheMirrorImage(string argumentText)
        {
            BigInteger argument = BigInteger.Parse(argumentText, CultureInfo.InvariantCulture);
            BigInteger value = argument + 1;
            byte[] negative = Item(1, argument);

            // 2^64 itself has no unsigned encoding; the others pair up with the negative item above.
            if (value >= TwoPow64)
            {
                return;
            }

            byte[] positive = Item(0, value);

            Assert.All(ReadAll<double>(positive), read => Assert.Equal(ExactDouble(value), read));
            Assert.All(ReadAll<float>(positive), read => Assert.Equal(ExactSingle(value), read));
            Assert.All(ReadAll<Half>(positive), read => Assert.Equal(ExactHalf(value), read));
            Assert.All(ReadAll<decimal>(positive), read => Assert.Equal((decimal)value, read));

            // and the negative one is exactly its negation, in every type
            Assert.Equal(-ReadAll<double>(positive).First(), ReadAll<double>(negative).First());
            Assert.Equal(-ReadAll<float>(positive).First(), ReadAll<float>(negative).First());
            Assert.Equal(-ReadAll<Half>(positive).First(), ReadAll<Half>(negative).First());
            Assert.Equal(-ReadAll<decimal>(positive).First(), ReadAll<decimal>(negative).First());
        }

        [Fact]
        public void TheBoundaryValuesAreWhatTheirNamesSay()
        {
            // -2^63 - 1, which a long cannot hold, is below zero in every floating type
            byte[] item = "3B8000000000000000".HexToBytes();

            Assert.Equal(-9.223372036854776E+18, Cbor.Deserialize<double>(item));
            Assert.Equal(-9.223372E+18f, Cbor.Deserialize<float>(item));
            Assert.Equal(Half.NegativeInfinity, Cbor.Deserialize<Half>(item));
            Assert.Equal(-9223372036854775809m, Cbor.Deserialize<decimal>(item));

            // -2^64
            item = "3BFFFFFFFFFFFFFFFF".HexToBytes();

            Assert.Equal(-1.8446744073709552E+19, Cbor.Deserialize<double>(item));
            Assert.Equal(-1.8446744E+19f, Cbor.Deserialize<float>(item));
            Assert.Equal(Half.NegativeInfinity, Cbor.Deserialize<Half>(item));
            Assert.Equal(-18446744073709551616m, Cbor.Deserialize<decimal>(item));
        }

        // ---- decimal from a float it cannot hold ----------------------------------------------------

        [Theory]
        [InlineData("F97E00")]                  // half NaN
        [InlineData("F97C00")]                  // half +Infinity
        [InlineData("F9FC00")]                  // half -Infinity
        [InlineData("FA7FC00000")]              // single NaN
        [InlineData("FA7F800000")]              // single +Infinity
        [InlineData("FAFF800000")]              // single -Infinity
        [InlineData("FA6F800000")]              // single 2^96, the first it cannot hold
        [InlineData("FAEF800000")]              // single -2^96
        [InlineData("FA7149F2CA")]              // single 1e30
        [InlineData("FA7F7FFFFF")]              // single float.MaxValue
        [InlineData("FB7FF8000000000000")]      // double NaN
        [InlineData("FB7FF0000000000000")]      // double +Infinity
        [InlineData("FBFFF0000000000000")]      // double -Infinity
        [InlineData("FB45F0000000000000")]      // double 2^96
        [InlineData("FBC5F0000000000000")]      // double -2^96
        [InlineData("FB45F431E0FAE6D721")]      // double 1e29
        [InlineData("FB7E37E43C8800759C")]      // double 1e300
        [InlineData("FBFE37E43C8800759C")]      // double -1e300
        [InlineData("FB7FEFFFFFFFFFFFFF")]      // double.MaxValue
        public void ADecimalReadFromAFloatItCannotHoldIsACborException(string hex)
        {
            AssertRefused<decimal>(hex.HexToBytes());
        }

        [Fact]
        public void TheRefusalNamesTheValue()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<decimal>("FB7FF8000000000000".HexToBytes()));
            Assert.Contains("NaN", exception.Message);

            exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<decimal>("FB7E37E43C8800759C".HexToBytes()));
            Assert.Contains("1E+300", exception.Message);

            exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<decimal>("FAFF800000".HexToBytes()));
            Assert.Contains("-Infinity", exception.Message);

            exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<decimal>("F97C00".HexToBytes()));
            Assert.Contains("Infinity", exception.Message);
        }

        [Fact]
        public void TheRefusalReachesTheObjectModel()
        {
            // A decimal in a CborValue is read by the same code
            byte[] item = ("FC" + "00000000" + "00000001" + "00000000" + "001D0000").HexToBytes();

            Assert.Equal("$", Assert.Throws<CborException>(() => Cbor.Deserialize<CborValue>(item)).Path);
        }

        [Theory]
        [InlineData("F97BFF", "65504")]
        [InlineData("F9FBFF", "-65504")]
        [InlineData("F90000", "0")]
        [InlineData("FA6F7FFFFF", "79228160000000000000000000000")]            // the largest single below 2^96
        [InlineData("FAEF7FFFFF", "-79228160000000000000000000000")]
        [InlineData("FB45EFFFFFFFFFFFFF", "79228162514264300000000000000")]    // the largest double below 2^96
        [InlineData("FBC5EFFFFFFFFFFFFF", "-79228162514264300000000000000")]
        [InlineData("FB0000000000000001", "0")]                                  // double.Epsilon underflows to zero
        [InlineData("FB3FF8000000000000", "1.5")]
        [InlineData("FA3FC00000", "1.5")]
        public void ADecimalReadFromAFloatItCanHoldIsUnchanged(string hex, string expected)
        {
            decimal expectedValue = decimal.Parse(expected, CultureInfo.InvariantCulture);

            Assert.All(ReadAll<decimal>(hex.HexToBytes()), value => Assert.Equal(expectedValue, value));
        }

        // ---- decimal in its raw form ----------------------------------------------------------------

        private static string Raw(string mid, string low, string high, string flags)
        {
            return "FC" + mid + low + high + flags;
        }

        [Theory]
        [InlineData("00000000", 1, 0)]
        [InlineData("00020000", 1, 2)]
        [InlineData("001C0000", 1, 28)]
        [InlineData("80000000", -1, 0)]
        [InlineData("80010000", -1, 1)]
        [InlineData("801C0000", -1, 28)]
        public void ARawDecimalWithValidFlagsIsRead(string flags, int sign, int scale)
        {
            decimal expected = new decimal(1, 0, 0, sign < 0, (byte)scale);

            Assert.All(ReadAll<decimal>(Raw("00000000", "00000001", "00000000", flags).HexToBytes()),
                value => Assert.Equal(expected, value));
        }

        [Theory]
        [InlineData("001D0000")] // scale 29
        [InlineData("00FF0000")] // scale 255
        [InlineData("00000001")] // reserved low bits
        [InlineData("0000FFFF")]
        [InlineData("00010001")]
        [InlineData("01000000")] // reserved high bits
        [InlineData("7F000000")]
        [InlineData("80000001")]
        [InlineData("FFFFFFFF")]
        public void ARawDecimalWithInvalidFlagsIsACborException(string flags)
        {
            AssertRefused<decimal>(Raw("00000000", "00000001", "00000000", flags).HexToBytes());
            AssertRefused<decimal>(Raw("FFFFFFFF", "FFFFFFFF", "FFFFFFFF", flags).HexToBytes());
        }
    }
}
