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
        /// Rounding is delegated rather than hand-rolled. <c>Mantissa E Exponent</c> is an exact
        /// rendering of this value in the notation <see cref="double.Parse(string, NumberStyles,
        /// IFormatProvider)"/> reads, so the conversion carries exactly one rounding step -- the
        /// platform's own decimal-to-binary one -- where composing powers of ten in floating point
        /// would round twice and hand-rolling the round-to-nearest would be a numeric kernel to get
        /// wrong. On .NET Core 3.0 and later that step is correctly rounded; on the .NET Framework a
        /// <c>netstandard2.0</c> consumer reaches an older parser that may differ in the last place.
        /// <para>
        /// The rendering is capped, because <see cref="BigInteger.ToString()"/> is a base conversion and
        /// so quadratic in the digit count: an 80,000-digit mantissa took 93 ms to render against 5 ms
        /// for 20,000, and the cost is paid on data a caller decoded rather than on anything it chose.
        /// The cap cannot change the answer -- see <see cref="SignificantDigitsForDouble"/>.
        /// </para>
        /// </remarks>
        public double ToDouble()
        {
            (BigInteger mantissa, int exponent) = WithDigitsCappedForDouble();

            string exact = mantissa.ToString(CultureInfo.InvariantCulture)
                + "E"
                + exponent.ToString(CultureInfo.InvariantCulture);

            // Older parsers throw where newer ones saturate, so both outcomes have to become the same
            // OverflowException.
            double result;

            try
            {
                result = double.Parse(exact, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                throw new OverflowException(
                    $"A decimal fraction with exponent {Exponent} is outside the range of Double.");
            }

            if (double.IsInfinity(result))
            {
                throw new OverflowException(
                    $"A decimal fraction with exponent {Exponent} is outside the range of Double.");
            }

            return result;
        }

        /// <summary>
        /// Significant digits kept when rendering for <see cref="ToDouble"/>. Comfortably more than any
        /// answer can depend on, and enough that the cap is provably invisible.
        /// </summary>
        /// <remarks>
        /// Which <see cref="double"/> a decimal value rounds to is decided by where it falls relative to
        /// the midpoints between adjacent doubles. Every such midpoint is a dyadic rational -- an odd
        /// multiple of 2^-1075 at the smallest -- and its exact decimal expansion therefore has at most
        /// about 770 significant digits. So a midpoint cannot agree with a value in its first 1,200
        /// significant digits and then differ beyond them: there are no digits left for it to differ in.
        /// Truncating past 1,200 digits and marking that something was dropped leaves the rounded result
        /// identical, and the marking is what keeps a value that was <em>above</em> a midpoint above it.
        /// </remarks>
        private const int SignificantDigitsForDouble = 1200;

        /// <summary>
        /// This value with no more than <see cref="SignificantDigitsForDouble"/> significant digits: as
        /// it stands where it already has fewer, and otherwise truncated with a trailing non-zero digit
        /// standing for everything dropped.
        /// </summary>
        /// <remarks>
        /// The digit count comes from <see cref="BigInteger.Log10"/>, which reads the leading bits rather
        /// than every digit, so nothing is rendered to find out how long it is. One
        /// <see cref="BigInteger.DivRem"/> does the truncation. An exact truncation -- every dropped
        /// digit a zero -- needs no marker and is left exact.
        /// </remarks>
        private (BigInteger Mantissa, int Exponent) WithDigitsCappedForDouble()
        {
            if (Mantissa.IsZero)
            {
                return (Mantissa, Exponent);
            }

            BigInteger magnitude = BigInteger.Abs(Mantissa);
            long digits = (long)BigInteger.Log10(magnitude) + 1;

            if (digits <= SignificantDigitsForDouble)
            {
                return (Mantissa, Exponent);
            }

            long drop = digits - SignificantDigitsForDouble;

            // The exponent grows by what the mantissa loses, so the value is unchanged apart from the
            // digits dropped. It cannot overflow: dropping digits only ever moves the exponent towards
            // zero from below, and a positive exponent this large is refused by the range check either
            // way once the parse sees it.
            BigInteger kept = BigInteger.DivRem(
                Mantissa, BigInteger.Pow(10, (int)drop), out BigInteger dropped);
            long exponent = Exponent + drop;

            if (dropped.IsZero)
            {
                return (kept, (int)Math.Min(exponent, int.MaxValue));
            }

            // A non-zero remainder means the true value is strictly beyond the truncation, so one
            // non-zero digit is appended to say so rather than leaving it looking exact -- otherwise a
            // value just above a midpoint would round as though it were on it.
            BigInteger marked = kept * 10 + (kept.Sign < 0 ? -1 : 1);

            return (marked, (int)Math.Min(exponent - 1, int.MaxValue));
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
