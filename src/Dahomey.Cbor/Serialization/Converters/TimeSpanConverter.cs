using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="TimeSpan"/> as an RFC 9581 §4 duration, tag 1002.
    /// </summary>
    /// <remarks>
    /// Registered only when <see cref="CborOptions.TimeSpanFormat"/> asks for it. Under the default
    /// <see cref="TimeSpanFormat.Members"/> there is no converter for a <see cref="TimeSpan"/> at all
    /// and the object mapping handles it, exactly as it always has -- which is what keeps this addition
    /// from changing any document already in circulation.
    /// </remarks>
    public class TimeSpanConverter : CborConverterBase<TimeSpan>
    {
        /// <summary>RFC 9581's tag for a duration.</summary>
        public const ulong DurationTag = 1002;

        /// <summary>The base value, in seconds. RFC 9581 section 3.1's key 1, shared with tag 1001.</summary>
        private const int SecondsKey = 1;

        private const int DecimalFractionBaseKey = 4;
        private const int BigFloatBaseKey = 5;

        /// <summary>The timescale: -1 and -13 elective, 13 critical (section 3.4).</summary>
        private const int TimescaleElectiveKey = -1;
        private const int TimescaleElectiveAlternateKey = -13;
        private const int TimescaleCriticalKey = 13;

        private const int NanosecondsKey = -9;

        private const long NanosecondsPerTick = 100;

        public override TimeSpan Read(ref CborReader reader)
        {
            // A plain number is accepted as seconds. Not a form this writes, and not one RFC 9581
            // defines -- a duration is always a map there -- but it is the obvious shape for a peer that
            // never adopted the tag, and refusing it would buy nothing.
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    return FromWholeSeconds(reader.ReadInt64(), 0, 0);

                case CborDataItemType.Double:
                case CborDataItemType.Single:
                    return FromFloatSeconds(reader.ReadDouble());
            }

            reader.ReadBeginMap();

            int size = reader.ReadSize();

            bool hasSeconds = false;
            bool hasFloatSeconds = false;
            long seconds = 0;
            double floatSeconds = 0;
            bool hasFraction = false;
            long fractionSeconds = 0;
            long fractionTicks = 0;
            bool hasTimescale = false;

            for (int read = 0; size == -1 || read < size; read++)
            {
                if (size == -1 && reader.IsBreak())
                {
                    break;
                }

                if (!TryReadKey(ref reader, out int key))
                {
                    continue;
                }

                switch (key)
                {
                    case SecondsKey:
                        if (hasSeconds || hasFloatSeconds)
                        {
                            throw new CborException("A duration holds key 1 twice");
                        }

                        // Section 3.1: key 1 is "int or float", exactly as tag 1 is. A float base is
                        // only meaningful with no fraction key beside it (section 3.3).
                        if (reader.GetCurrentDataItemType() is CborDataItemType.Single or CborDataItemType.Double)
                        {
                            floatSeconds = reader.ReadDouble();
                            hasFloatSeconds = true;
                        }
                        else
                        {
                            seconds = reader.ReadInt64();
                            hasSeconds = true;
                        }
                        break;

                    case -3:
                        fractionTicks = ReadFraction(ref reader, ref hasFraction, 3, out fractionSeconds);
                        break;

                    case -6:
                        fractionTicks = ReadFraction(ref reader, ref hasFraction, 6, out fractionSeconds);
                        break;

                    case NanosecondsKey:
                        fractionTicks = ReadFraction(ref reader, ref hasFraction, 9, out fractionSeconds);
                        break;

                    case -12:
                        fractionTicks = ReadFraction(ref reader, ref hasFraction, 12, out fractionSeconds);
                        break;

                    case -15:
                        fractionTicks = ReadFraction(ref reader, ref hasFraction, 15, out fractionSeconds);
                        break;

                    case -18:
                        fractionTicks = ReadFraction(ref reader, ref hasFraction, 18, out fractionSeconds);
                        break;

                    case TimescaleElectiveKey:
                    case TimescaleElectiveAlternateKey:
                    case TimescaleCriticalKey:
                        // Section 3.4. A length in SI seconds is the same on UTC and TAI, so both are
                        // accepted; any other timescale is one this cannot vouch for. Keys -1 and -13
                        // are the *timescale* (not a floating-point fraction), at most one of the three.
                        if (hasTimescale)
                        {
                            throw new CborException("A duration holds more than one timescale key");
                        }

                        hasTimescale = true;

                        if (reader.GetCurrentDataItemType() != CborDataItemType.Unsigned
                            || reader.ReadUInt64() > 1)
                        {
                            throw new CborException("A duration in an unsupported timescale; only UTC (0) and TAI (1) are read");
                        }
                        break;

                    case DecimalFractionBaseKey:
                    case BigFloatBaseKey:
                        // Section 3.2: a base time as a decimal fraction or bigfloat. Not read here, and
                        // skipping it would drop the whole value.
                        throw new CborException(
                            $"A duration with its base under key {key} is not supported; use key 1");

                    default:
                        if (key >= 0)
                        {
                            // Section 3: an unsigned key is critical, and a critical key this does not
                            // understand means the value cannot be read faithfully.
                            throw new CborException($"A duration holds the unsupported critical key {key}");
                        }

                        // Elective: clock quality, uncertainty, guarantee and the like. None changes the
                        // length being described.
                        reader.SkipDataItem();
                        break;
                }
            }

            if (size == -1)
            {
                reader.ConsumeBreak();
            }

            if (hasFloatSeconds)
            {
                if (hasFraction)
                {
                    throw new CborException("A duration with a fraction key needs an integer under key 1");
                }

                return FromFloatSeconds(floatSeconds);
            }

            if (!hasSeconds)
            {
                throw new CborException("A duration needs a base under key 1");
            }

            return FromWholeSeconds(seconds, fractionSeconds, fractionTicks);
        }

        /// <summary>
        /// Reads a fraction key's unsigned count of 10^-<paramref name="exponent"/> seconds, returning
        /// the sub-second part in ticks and the whole seconds through <paramref name="wholeSeconds"/>.
        /// </summary>
        /// <remarks>
        /// The value is unsigned by definition (section 3.3), so a negative one is refused rather than
        /// read as a subtraction -- a sign there has no defined meaning. A TimeSpan resolves to 100ns,
        /// so a finer fraction is refused unless it is an exact multiple of a tick: truncating would
        /// make a peer's value silently read as a different duration. The whole seconds are kept apart
        /// from the ticks because a large fraction may be cancelled by a negative base, and must not be
        /// refused before it is added to it.
        /// </remarks>
        private static long ReadFraction(
            ref CborReader reader, ref bool hasFraction, int exponent, out long wholeSeconds)
        {
            if (hasFraction)
            {
                throw new CborException("A duration holds more than one fraction key");
            }

            hasFraction = true;

            if (reader.GetCurrentDataItemType() != CborDataItemType.Unsigned)
            {
                throw new CborException($"A duration's fraction key -{exponent} must be an unsigned integer");
            }

            ulong count = reader.ReadUInt64();

            // The smallest exponent is 3, so the whole seconds never exceed 2^64 / 1000 and fit a long.
            ulong unit = 1;
            for (int i = 0; i < exponent; i++)
            {
                unit *= 10;
            }

            wholeSeconds = (long)(count / unit);
            ulong remainder = count % unit;

            // Ticks are 10^-7 s. Coarser keys scale up, finer ones divide down.
            if (exponent <= 7)
            {
                ulong scale = 1;
                for (int i = exponent; i < 7; i++)
                {
                    scale *= 10;
                }

                return (long)(remainder * scale);
            }

            ulong divisor = 1;
            for (int i = 7; i < exponent; i++)
            {
                divisor *= 10;
            }

            if (remainder % divisor != 0)
            {
                throw new CborException(
                    $"A duration's fraction is finer than a TimeSpan's 100ns resolution ({count} x 1e-{exponent} s)");
            }

            return (long)(remainder / divisor);
        }

        /// <summary>
        /// Reads one key. A key this cannot represent as an int is elective (section 3: a text string
        /// or a negative integer) and is skipped together with its value, returning false; an unsigned
        /// one is a critical key this cannot understand, and a key of any other type is not a key.
        /// </summary>
        private static bool TryReadKey(ref CborReader reader, out int key)
        {
            key = 0;

            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Unsigned:
                    ulong unsigned = reader.ReadUInt64();

                    if (unsigned > int.MaxValue)
                    {
                        throw new CborException($"A duration holds the unsupported critical key {unsigned}");
                    }

                    key = (int)unsigned;
                    return true;

                case CborDataItemType.Signed:
                    CborReaderBookmark bookmark = reader.GetBookmark();
                    long signed;

                    try
                    {
                        signed = reader.ReadInt64();
                    }
                    catch (CborException)
                    {
                        // Below -2^63, which no long holds; such a key is none this defines.
                        reader.ReturnToBookmark(bookmark);
                        reader.SkipDataItem();
                        reader.SkipDataItem();
                        return false;
                    }

                    if (signed < int.MinValue)
                    {
                        reader.SkipDataItem();
                        return false;
                    }

                    key = (int)signed;
                    return true;

                case CborDataItemType.String:
                    reader.SkipDataItem();
                    reader.SkipDataItem();
                    return false;

                default:
                    throw new CborException("A duration's keys must be integers or text strings");
            }
        }

        /// <summary>
        /// <c>seconds + fraction</c>, the fraction being non-negative (section 3.3), checked against the
        /// range of a <see cref="TimeSpan"/>.
        /// </summary>
        private static TimeSpan FromWholeSeconds(long seconds, long fractionSeconds, long fractionTicks)
        {
            try
            {
                // The fraction's whole seconds are added to the base first, so that what is multiplied
                // by TicksPerSecond below is the final number of seconds and the sub-second part is
                // always in [0, 1 s). Otherwise an intermediate product could overflow although the
                // sum is in range.
                seconds = checked(seconds + fractionSeconds);

                long ticks;

                if (seconds < 0)
                {
                    // seconds * 1e7 alone can fall below long.MinValue while the sum with a positive
                    // fraction does not -- TimeSpan.MinValue is exactly that case -- so one second is
                    // moved from the first term to the second.
                    ticks = checked((seconds + 1) * TimeSpan.TicksPerSecond
                        + (fractionTicks - TimeSpan.TicksPerSecond));
                }
                else
                {
                    ticks = checked(seconds * TimeSpan.TicksPerSecond + fractionTicks);
                }

                return TimeSpan.FromTicks(ticks);
            }
            catch (OverflowException)
            {
                throw new CborException("A duration is out of range for a TimeSpan");
            }
        }

        /// <summary>
        /// A float base is inexact by nature, so it is rounded to the nearest tick; NaN, infinity and
        /// anything outside a <see cref="TimeSpan"/>'s range are refused.
        /// </summary>
        private static TimeSpan FromFloatSeconds(double seconds)
        {
            double ticks = Math.Round(seconds * TimeSpan.TicksPerSecond);

            if (double.IsNaN(ticks) || ticks < long.MinValue || ticks >= 9223372036854775808.0)
            {
                throw new CborException("A duration is out of range for a TimeSpan");
            }

            return TimeSpan.FromTicks((long)ticks);
        }

        public override void Write(ref CborWriter writer, TimeSpan value)
        {
            long ticks = value.Ticks;
            long seconds = ticks / TimeSpan.TicksPerSecond;
            long remainder = ticks % TimeSpan.TicksPerSecond;

            // Section 3.3 makes the fraction an unsigned count added to the base, so it can never be
            // negative: the base is floored, and -1.5s is {1: -2, -9: 500000000}.
            if (remainder < 0)
            {
                seconds--;
                remainder += TimeSpan.TicksPerSecond;
            }

            writer.WriteSemanticTag(DurationTag);

            if (remainder == 0)
            {
                writer.WriteBeginMap(1);
                writer.WriteInt32(SecondsKey);
                writer.WriteInt64(seconds);
                return;
            }

            writer.WriteBeginMap(2);
            writer.WriteInt32(SecondsKey);
            writer.WriteInt64(seconds);
            writer.WriteInt32(NanosecondsKey);
            writer.WriteInt64(remainder * NanosecondsPerTick);
        }
    }
}
