using System;
using System.Globalization;
using System.Numerics;

namespace Dahomey.Cbor
{
    /// <summary>
    /// An RFC 8949 §3.4.4 decimal fraction, tag 4: <see cref="Mantissa"/> × 10^<see cref="Exponent"/>.
    /// </summary>
    /// <remarks>
    /// A dedicated type rather than a mapping onto <see cref="decimal"/>, because the tag can express
    /// more than <see cref="decimal"/> holds -- an arbitrarily wide mantissa and an exponent well past
    /// a scale of 28 -- so reading into <see cref="decimal"/> would need a policy for what to do with
    /// the rest. Holding the whole of what the tag can carry means read and write are symmetric and
    /// there is no such policy: a caller wanting a decimal fraction asks for one by declaring this
    /// type, the way a caller wanting a bignum declares <see cref="BigInteger"/>.
    /// <para>
    /// Nothing here changes what <see cref="decimal"/>, <see cref="double"/>, <see cref="float"/> or
    /// <c>System.Half</c> read or write.
    /// </para>
    /// <para>
    /// Equality is structural, over the pair as encoded. So <c>1e1</c> and <c>10e0</c> are the same
    /// number and are <em>not</em> equal, and neither is normalised on the way out: a document saying
    /// <c>10e0</c> round-trips as <c>10e0</c>. That is the defensible answer for a type whose purpose
    /// is to carry what the wire said -- normalising would make the type lossy in exactly the way
    /// mapping onto <see cref="decimal"/> would -- but it means two encodings of one number are two
    /// dictionary keys, and under <c>CborOptions.Deterministic</c> they sort as the different byte
    /// strings they are.
    /// </para>
    /// </remarks>
    public readonly struct CborDecimalFraction : IEquatable<CborDecimalFraction>
    {
        /// <summary>The largest magnitude <see cref="decimal"/> holds: 2^96 - 1.</summary>
        private static readonly BigInteger MaxDecimalMagnitude = (BigInteger.One << 96) - BigInteger.One;

        public CborDecimalFraction(BigInteger mantissa, int exponent)
        {
            Mantissa = mantissa;
            Exponent = exponent;
        }

        /// <summary>The significand, which RFC 8949 §3.4.4 allows to be a bignum.</summary>
        public BigInteger Mantissa { get; }

        /// <summary>
        /// The power of ten the mantissa is scaled by. RFC 8949 §3.4.4 requires a basic integer here
        /// rather than a bignum, so the wire form reaches ±2^64; this holds an <see cref="int"/>, and
        /// an exponent outside that is refused on read as a <see cref="CborException"/>. That is a real
        /// narrowing of what the format permits rather than a case where nothing is lost, and it is
        /// deliberate: an exponent beyond ±2^31 describes a number with more digits than there is
        /// memory to render.
        /// </summary>
        public int Exponent { get; }

        /// <summary>
        /// The exact <see cref="decimal"/> with this value, or <see cref="OverflowException"/> if there
        /// is none. Never rounds.
        /// </summary>
        /// <remarks>
        /// An exponent below -28 is not immediately fatal: <see cref="decimal"/> carries a scale of 0
        /// to 28, and a mantissa with trailing zeros can give the difference back, so <c>1000e-30</c>
        /// is the representable <c>1e-27</c>. What cannot be represented -- a mantissa wider than 96
        /// bits, or a scale that will not reduce to 28 -- throws rather than rounding, since a silent
        /// rounding here is the precision loss this type exists to avoid.
        /// <para>
        /// The reduction is one division rather than one per digit, and the difference is not
        /// micro-optimisation: each division is O(n) in a mantissa that came off the wire, so removing
        /// digits one at a time is quadratic in the document's own size. A 66 KB mantissa took 20
        /// seconds that way against 19 ms like this. <c>BigInteger.Log10</c> bounds the digit count
        /// before <c>BigInteger.Pow</c> is handed anything, so the power of ten follows the mantissa
        /// actually present rather than an exponent the document merely claims -- the same shape as
        /// <c>CborReader.DivideOutFactorsOfTen</c>, which reads tag 4 into a <see cref="decimal"/>.
        /// </para>
        /// </remarks>
        public decimal ToDecimal()
        {
            BigInteger mantissa = Mantissa;
            int exponent = Exponent;

            if (mantissa.IsZero)
            {
                return decimal.Zero;
            }

            if (exponent < -28)
            {
                long excess = -28L - exponent;
                long digits = (long)BigInteger.Log10(BigInteger.Abs(mantissa)) + 1;

                // A value cannot give up more factors of ten than it has digits, so anything past that
                // is out of range without dividing at all.
                if (excess <= digits)
                {
                    BigInteger divisor = BigInteger.Pow(10, (int)excess);
                    BigInteger reduced = BigInteger.DivRem(mantissa, divisor, out BigInteger remainder);

                    // A non-zero remainder means one of the digits dropped is part of the value, so the
                    // scale cannot be given back and the check below refuses the value. All-or-nothing
                    // is exact here: a partial reduction would leave the scale past 28 regardless.
                    if (remainder.IsZero)
                    {
                        mantissa = reduced;
                        exponent = -28;
                    }
                }
            }

            if (exponent > 0)
            {
                // There is no scale to spend on a positive exponent, so it folds into the mantissa. The
                // bound comes first: 10^29 is already past decimal.MaxValue for a mantissa of 1, so
                // anything above it overflows whatever the mantissa is, and BigInteger.Pow never sees a
                // number a hostile document merely claimed.
                if (exponent > 28)
                {
                    throw new OverflowException(
                        $"A decimal fraction with exponent {Exponent} is outside the range of Decimal.");
                }

                mantissa *= BigInteger.Pow(10, exponent);
                exponent = 0;
            }

            if (exponent < -28)
            {
                throw new OverflowException(
                    $"A decimal fraction with exponent {Exponent} is outside the range of Decimal.");
            }

            // A mantissa too wide for 96 bits is the same operation seen from the other end: dividing it
            // by ten while giving one back to the scale leaves the value alone. [-1, 10^29] arrives
            // inside the scale limit and still too wide, and is the 1E+28 a decimal holds perfectly well
            // at a scale of 0. Bounded by the scale, so at most 28 divisions rather than a search, and a
            // digit that is not zero leaves on the first one -- the value is then out of range, which
            // the check below reports.
            //
            // This mirrors CborReader.DecimalFromDecimalFraction deliberately: the same document read
            // into a decimal member and converted through this method has to give the same answer.
            while (exponent < 0 && !(BigInteger.Abs(mantissa) >> 96).IsZero)
            {
                BigInteger reduced = BigInteger.DivRem(mantissa, 10, out BigInteger remainder);

                if (!remainder.IsZero)
                {
                    break;
                }

                mantissa = reduced;
                exponent++;
            }

            BigInteger magnitude = BigInteger.Abs(mantissa);

            if (magnitude > MaxDecimalMagnitude)
            {
                throw new OverflowException(
                    "A decimal fraction whose mantissa is wider than 96 bits is outside the range of Decimal.");
            }

            return new decimal(
                unchecked((int)(uint)(magnitude & uint.MaxValue)),
                unchecked((int)(uint)((magnitude >> 32) & uint.MaxValue)),
                unchecked((int)(uint)((magnitude >> 64) & uint.MaxValue)),
                mantissa.Sign < 0,
                checked((byte)(-exponent)));
        }

        /// <summary>
        /// The nearest <see cref="double"/> to this value, or <see cref="OverflowException"/> if the
        /// magnitude is past what a <see cref="double"/> holds. Rounds, unlike
        /// <see cref="ToDecimal"/>.
        /// </summary>
        /// <remarks>
        /// Done in integer arithmetic rather than by rendering the value and letting
        /// <c>double.Parse</c> round it, which is what this did first and is not sound across the
        /// framework versions this library targets: on .NET 8 a 1,201-digit mantissa at
        /// <c>E-1200</c> -- a value just above 1.0 -- parses as **zero**, where .NET 9 and .NET 10
        /// return the right answer. Delegating the rounding also delegated that, so the conversion now
        /// owns it: one exact division and one rounding step, identical on every target.
        /// <para>
        /// The value is <c>num / den</c> with both sides exact, aligned so the quotient carries one bit
        /// more than a significand needs, and rounded to nearest with ties to even -- the remainder and
        /// any bit shifted out together saying whether anything followed. The powers of ten are bounded
        /// by the mantissa in hand before either is built, so an exponent a document merely claims
        /// cannot ask for one the value does not need.
        /// </para>
        /// </remarks>
        public double ToDouble()
        {
            if (Mantissa.IsZero)
            {
                return 0.0;
            }

            bool negative = Mantissa.Sign < 0;
            BigInteger magnitude = BigInteger.Abs(Mantissa);

            // Decimal magnitude of the value, which settles the two extremes without any arithmetic:
            // 10^309 is past double.MaxValue and 10^-324 is below the smallest subnormal, so a value
            // clear of both ends needs no exact treatment to answer.
            long decimalMagnitude = (long)BigInteger.Log10(magnitude) + 1 + Exponent;

            if (decimalMagnitude > 310)
            {
                throw new OverflowException(
                    $"A decimal fraction with exponent {Exponent} is outside the range of Double.");
            }

            if (decimalMagnitude < -340)
            {
                return negative ? -0.0 : 0.0;
            }

            BigInteger numerator = magnitude;
            BigInteger denominator = BigInteger.One;

            if (Exponent >= 0)
            {
                numerator *= BigInteger.Pow(10, Exponent);
            }
            else
            {
                denominator = BigInteger.Pow(10, -Exponent);
            }

            return RoundToDouble(numerator, denominator, negative, Exponent);
        }

        /// <summary>
        /// The nearest <see cref="double"/> to <paramref name="numerator"/> /
        /// <paramref name="denominator"/>, both positive, rounding ties to even.
        /// </summary>
        /// <remarks>
        /// The quotient is taken with 54 significant bits -- 53 for the significand and one to round on
        /// -- and the division's remainder says whether anything followed it, which is what separates a
        /// value exactly on a midpoint from one just above it. Near zero the grid is coarser than 53
        /// bits, because a subnormal's lowest bit is 2^-1074 whatever its magnitude; the alignment is
        /// widened to that grid before rounding rather than after, so the result is rounded once instead
        /// of twice.
        /// </remarks>
        private static double RoundToDouble(
            BigInteger numerator, BigInteger denominator, bool negative, int exponentForMessage)
        {
            const int SignificandBits = 53;
            const int MinimumSubnormalExponent = -1074;

            // Where the quotient's leading bit falls, to within one place: enough to align on, and the
            // normalisation below settles the ambiguity.
            long alignment = BitLength(numerator) - BitLength(denominator) - (SignificandBits + 1);

            // A subnormal result cannot carry 54 bits below 2^-1074, so the grid rather than the
            // precision decides where to round. One place below the grid, not on it: the rounding step
            // spends a bit, and clamping to the grid itself would spend the value's last one --
            // double.Epsilon would come back as zero.
            if (alignment < MinimumSubnormalExponent - 1)
            {
                alignment = MinimumSubnormalExponent - 1;
            }

            BigInteger shiftedNumerator = numerator;
            BigInteger shiftedDenominator = denominator;

            if (alignment > 0)
            {
                shiftedDenominator <<= (int)alignment;
            }
            else
            {
                shiftedNumerator <<= (int)-alignment;
            }

            BigInteger quotient = BigInteger.DivRem(
                shiftedNumerator, shiftedDenominator, out BigInteger remainder);
            bool anythingFollowed = !remainder.IsZero;

            // The estimate can leave one bit too many, and for a subnormal it leaves however many the
            // grid demands. Bits shifted out here join the remainder in saying something followed.
            long excessBits = BitLength(quotient) - (SignificandBits + 1);

            if (excessBits > 0)
            {
                BigInteger dropped = quotient & ((BigInteger.One << (int)excessBits) - 1);

                anythingFollowed |= !dropped.IsZero;
                quotient >>= (int)excessBits;
                alignment += excessBits;
            }

            // Round the last bit away: up when what follows is more than half, to even when exactly
            // half, down otherwise.
            bool roundBitSet = !(quotient & BigInteger.One).IsZero;
            quotient >>= 1;
            alignment++;

            if (roundBitSet && (anythingFollowed || !(quotient & BigInteger.One).IsZero))
            {
                quotient += BigInteger.One;

                // Carrying past the significand's width takes a bit back from the exponent.
                if (BitLength(quotient) > SignificandBits)
                {
                    quotient >>= 1;
                    alignment++;
                }
            }

            double result = ScaleByPowerOfTwo((double)quotient, alignment);

            if (double.IsInfinity(result))
            {
                throw new OverflowException(
                    $"A decimal fraction with exponent {exponentForMessage} is outside the range of Double.");
            }

            return negative ? -result : result;
        }

        /// <summary>
        /// <paramref name="value"/> × 2^<paramref name="exponent"/>, exactly where the result is
        /// normal.
        /// </summary>
        /// <remarks>
        /// A power of two is exactly representable, so a single multiplication carries no rounding of
        /// its own -- but only while both the power and the result stay normal. Below that the power is
        /// applied in two steps, which is exact for the same reason: the caller has already rounded to
        /// the subnormal grid, so nothing is left to lose.
        /// </remarks>
        private static double ScaleByPowerOfTwo(double value, long exponent)
        {
            const int SmallestNormalExponent = -1022;

            if (exponent < SmallestNormalExponent)
            {
                double halfWay = Math.Pow(2.0, SmallestNormalExponent);

                return value * halfWay * Math.Pow(2.0, (double)(exponent - SmallestNormalExponent));
            }

            return value * Math.Pow(2.0, (double)exponent);
        }

        /// <summary>
        /// Bits in a positive <see cref="BigInteger"/>. <c>netstandard2.0</c> has no
        /// <c>GetBitLength</c>, so it counts from the bytes.
        /// </summary>
        private static long BitLength(BigInteger value)
        {
            if (value.IsZero)
            {
                return 0;
            }

#if NET8_0_OR_GREATER
            return (long)value.GetBitLength();
#else
            byte[] bytes = value.ToByteArray();
            int index = bytes.Length - 1;

            while (index > 0 && bytes[index] == 0)
            {
                index--;
            }

            long bits = (long)index * 8;

            for (byte top = bytes[index]; top != 0; top >>= 1)
            {
                bits++;
            }

            return bits;
#endif
        }

        /// <summary>
        /// The exact decimal fraction with the value of <paramref name="value"/>. Explicit rather than
        /// implicit so no conversion happens where one was not asked for.
        /// </summary>
        /// <remarks>
        /// Exact in both directions: a <see cref="decimal"/> is a 96-bit integer over a power of ten
        /// already, which is what this type is.
        /// </remarks>
        public static explicit operator CborDecimalFraction(decimal value)
        {
            int[] bits = decimal.GetBits(value);
            BigInteger magnitude = (BigInteger)(uint)bits[0]
                | ((BigInteger)(uint)bits[1] << 32)
                | ((BigInteger)(uint)bits[2] << 64);
            bool negative = (bits[3] & unchecked((int)0x80000000)) != 0;
            int scale = (bits[3] >> 16) & 0xFF;

            return new CborDecimalFraction(negative ? -magnitude : magnitude, -scale);
        }

        /// <summary>
        /// The exact decimal fraction with the value of <paramref name="value"/>, or
        /// <see cref="OverflowException"/> if it is not finite.
        /// </summary>
        /// <remarks>
        /// Exact, which is worth stating because it looks as though it could not be: a
        /// <see cref="double"/> is <c>m × 2^e</c>, and <c>2^-n = 5^n × 10^-n</c>, so every finite
        /// <see cref="double"/> is some decimal fraction. It is not the shortest one -- <c>0.1</c> is
        /// precisely <c>3602879701896397 × 2^-55</c> and so lands with a 55-digit mantissa rather than
        /// as <c>1e-1</c>. Exactness is the point; a caller wanting <c>1e-1</c> has a
        /// <see cref="decimal"/> to convert from, or the constructor.
        /// </remarks>
        public static explicit operator CborDecimalFraction(double value)
        {
            (BigInteger mantissa, int exponent) = CborBigFloat.DecomposeDouble(value, nameof(CborDecimalFraction));

            if (exponent >= 0)
            {
                // A shift rather than a power of two multiplied out, which is the same value without
                // building the power first.
                return new CborDecimalFraction(mantissa << exponent, 0);
            }

            return new CborDecimalFraction(mantissa * BigInteger.Pow(5, -exponent), exponent);
        }

        public bool Equals(CborDecimalFraction other)
        {
            return Mantissa == other.Mantissa && Exponent == other.Exponent;
        }

        public override bool Equals(object? obj)
        {
            return obj is CborDecimalFraction other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent;
            }
        }

        public static bool operator ==(CborDecimalFraction left, CborDecimalFraction right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CborDecimalFraction left, CborDecimalFraction right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// <c>27315E-2</c>: the pair as encoded, in the notation <see cref="ToDouble"/> parses, so what
        /// a failing test prints is the value on the wire rather than a rendering of it.
        /// </summary>
        public override string ToString()
        {
            return Mantissa.ToString(CultureInfo.InvariantCulture)
                + "E"
                + Exponent.ToString(CultureInfo.InvariantCulture);
        }
    }
}
