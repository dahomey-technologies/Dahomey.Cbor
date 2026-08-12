using System;
using System.Globalization;
using System.Numerics;

namespace Dahomey.Cbor
{
    /// <summary>
    /// An RFC 8949 §3.4.4 bigfloat, tag 5: <see cref="Mantissa"/> × 2^<see cref="Exponent"/>.
    /// </summary>
    /// <remarks>
    /// The same reasoning as <see cref="CborDecimalFraction"/>, in base two, and a separate type rather
    /// than one carrying its base as data: which tag a value is written under is then a compile-time
    /// fact, which is what decoding these tags semantically ought to buy. Equality is structural over
    /// the pair as encoded, and nothing is normalised on the way out -- see
    /// <see cref="CborDecimalFraction"/> for what that costs and why it is the right answer here.
    /// </remarks>
    public readonly struct CborBigFloat : IEquatable<CborBigFloat>
    {
        public CborBigFloat(BigInteger mantissa, int exponent)
        {
            Mantissa = mantissa;
            Exponent = exponent;
        }

        /// <summary>The significand, which RFC 8949 §3.4.4 allows to be a bignum.</summary>
        public BigInteger Mantissa { get; }

        /// <summary>
        /// The power of two the mantissa is scaled by, narrowed to <see cref="int"/> from the ±2^64 the
        /// wire form permits -- see <see cref="CborDecimalFraction.Exponent"/>, which documents that
        /// narrowing and why it is deliberate.
        /// </summary>
        public int Exponent { get; }

        /// <summary>
        /// The exact <see cref="decimal"/> with this value, or <see cref="OverflowException"/> if there
        /// is none. Never rounds.
        /// </summary>
        /// <remarks>
        /// Factors of two come out of the mantissa first, and that is what makes the answer exact rather
        /// than approximately right: <c>2^1000 × 2^-1000</c> is 1, so a large negative exponent does not
        /// by itself put a value out of reach. Once the mantissa is odd, a negative exponent of -n needs
        /// a scale of exactly n -- <c>5^n × odd</c> has no factor of ten to give back -- so the test
        /// against <see cref="decimal"/>'s 28 digits is exact, and it also bounds the power of five
        /// before it is computed.
        /// </remarks>
        public decimal ToDecimal()
        {
            if (Mantissa.IsZero)
            {
                return decimal.Zero;
            }

            (BigInteger mantissa, int exponent) = WithFactorsOfTwoRemoved();

            if (exponent < -28)
            {
                throw new OverflowException(
                    $"A bigfloat with exponent {Exponent} is outside the range of Decimal.");
            }

            if (exponent > 96)
            {
                throw new OverflowException(
                    $"A bigfloat with exponent {Exponent} is outside the range of Decimal.");
            }

            if (exponent > 0)
            {
                return new CborDecimalFraction(mantissa << exponent, 0).ToDecimal();
            }

            return new CborDecimalFraction(mantissa * BigInteger.Pow(5, -exponent), exponent).ToDecimal();
        }

        /// <summary>
        /// The nearest <see cref="double"/> to this value, <see cref="OverflowException"/> if the
        /// magnitude is past what a <see cref="double"/> holds, and zero if it is below the smallest
        /// subnormal. Rounds, unlike <see cref="ToDecimal"/>.
        /// </summary>
        /// <remarks>
        /// Converted exactly into a decimal fraction and rounded once there, since <c>2^-n = 5^n ×
        /// 10^-n</c>. The magnitude is bounded first, from the width of the mantissa and the exponent,
        /// so a hostile exponent cannot ask for a power of five the value does not need -- the bound is
        /// loose, taking the mantissa's width from its byte count, and the exact conversion below
        /// settles anything near the edge. What remains is proportional to the mantissa the caller
        /// already holds.
        /// </remarks>
        public double ToDouble()
        {
            if (Mantissa.IsZero)
            {
                return 0.0;
            }

            long magnitude = (long)Mantissa.ToByteArray().Length * 8 + Exponent;

            if (magnitude > 1100)
            {
                throw new OverflowException(
                    $"A bigfloat with exponent {Exponent} is outside the range of Double.");
            }

            if (magnitude < -1200)
            {
                return 0.0;
            }

            (BigInteger mantissa, int exponent) = WithFactorsOfTwoRemoved();

            if (exponent >= 0)
            {
                return new CborDecimalFraction(mantissa << exponent, 0).ToDouble();
            }

            return new CborDecimalFraction(mantissa * BigInteger.Pow(5, -exponent), exponent).ToDouble();
        }

        /// <summary>
        /// The exact bigfloat with the value of <paramref name="value"/>, or
        /// <see cref="OverflowException"/> if it is not finite. Exact because a <see cref="double"/> is
        /// a bigfloat -- an integer significand over a power of two -- which is why this direction needs
        /// no policy.
        /// </summary>
        public static explicit operator CborBigFloat(double value)
        {
            (BigInteger mantissa, int exponent) = DecomposeDouble(value, nameof(CborBigFloat));

            return new CborBigFloat(mantissa, exponent);
        }

        // No conversion from decimal, and its absence is a decision rather than an omission: it cannot
        // be exact. 0.1m is one tenth, and m × 2^e = 1/10 needs m = 2^e / 10, which is never an
        // integer. An operator that rounded there would be precisely the silent precision loss at a
        // language boundary that this type's explicit-only conversions exist to prevent. Convert to
        // CborDecimalFraction instead, which is exact, or build the value with the constructor.

        /// <summary>
        /// <paramref name="value"/> as an exact <c>mantissa × 2^exponent</c> pair.
        /// </summary>
        /// <remarks>
        /// Shared with <see cref="CborDecimalFraction"/>'s conversion from <see cref="double"/>, which
        /// re-bases the result. A subnormal has no implicit leading one and a fixed exponent, so it is
        /// taken apart on its own branch rather than by the general formula.
        /// <para>
        /// The pair is reduced to an odd mantissa, which is what makes the result the encoding §3.4.4
        /// itself shows: <c>1.5</c> is <c>[-1, 3]</c>, four bytes, where the significand as IEEE 754
        /// stores it would emit <c>[-52, 6755399441055744]</c> -- the same number in twelve. Every
        /// finite <see cref="double"/> has exactly one such form, so the conversion is deterministic as
        /// well as compact.
        /// </para>
        /// </remarks>
        internal static (BigInteger Mantissa, int Exponent) DecomposeDouble(double value, string targetType)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int biasedExponent = (int)((bits >> 52) & 0x7FF);
            long significand = bits & 0xFFFFFFFFFFFFFL;

            if (biasedExponent == 0x7FF)
            {
                throw new OverflowException(
                    $"{(double.IsNaN(value) ? "NaN" : "An infinity")} has no {targetType}.");
            }

            BigInteger mantissa;
            int exponent;

            if (biasedExponent == 0)
            {
                mantissa = significand;
                exponent = -1074;
            }
            else
            {
                mantissa = significand | (1L << 52);
                exponent = biasedExponent - 1075;
            }

            while (exponent < 0 && mantissa.IsEven)
            {
                mantissa >>= 1;
                exponent++;
            }

            return (negative ? -mantissa : mantissa, exponent);
        }

        /// <summary>
        /// The same value with the mantissa made odd, where a negative exponent allows it. Halving the
        /// mantissa and raising the exponent leaves the value alone, and it is what makes an integer
        /// written as <c>2^n × 2^-n</c> reachable by the conversions above.
        /// </summary>
        /// <remarks>
        /// One shift, by a count taken from the mantissa's own bytes, rather than one shift per bit:
        /// each shift is O(n) in a mantissa that came off the wire, so stripping bits one at a time is
        /// quadratic in the document's size, and <c>2^k × 2^-k</c> is a one-line document that reaches
        /// it. Trailing zero bits survive two's complement, so the count is the same for a negative
        /// mantissa and the byte array can be read directly.
        /// </remarks>
        private (BigInteger Mantissa, int Exponent) WithFactorsOfTwoRemoved()
        {
            BigInteger mantissa = Mantissa;
            int exponent = Exponent;

            if (exponent >= 0 || mantissa.IsZero || !mantissa.IsEven)
            {
                return (mantissa, exponent);
            }

            byte[] bytes = mantissa.ToByteArray();
            int zeroBytes = 0;

            while (zeroBytes < bytes.Length && bytes[zeroBytes] == 0)
            {
                zeroBytes++;
            }

            int shift = zeroBytes * 8;

            for (byte lowest = bytes[zeroBytes]; (lowest & 1) == 0; lowest >>= 1)
            {
                shift++;
            }

            // Only as far as the exponent allows: past zero the value would change.
            shift = Math.Min(shift, -exponent);

            return (mantissa >> shift, exponent + shift);
        }

        public bool Equals(CborBigFloat other)
        {
            return Mantissa == other.Mantissa && Exponent == other.Exponent;
        }

        public override bool Equals(object? obj)
        {
            return obj is CborBigFloat other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent;
            }
        }

        public static bool operator ==(CborBigFloat left, CborBigFloat right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CborBigFloat left, CborBigFloat right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// <c>3*2^-1</c>: the pair as encoded. Not <c>E</c> notation, which would read as a power of
        /// ten and so as a different number.
        /// </summary>
        public override string ToString()
        {
            return Mantissa.ToString(CultureInfo.InvariantCulture)
                + "*2^"
                + Exponent.ToString(CultureInfo.InvariantCulture);
        }
    }
}
