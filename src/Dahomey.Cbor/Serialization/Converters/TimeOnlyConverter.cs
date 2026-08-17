#if NET6_0_OR_GREATER
using System;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="TimeOnly"/> as an RFC 3339 <c>partial-time</c> string, or as a
    /// count of seconds since midnight.
    /// </summary>
    /// <remarks>
    /// Deliberately untagged, unlike <see cref="DateOnlyConverter"/>. The CBOR tag registry has
    /// nothing for a time of day -- tags 0, 1, 4 and 5 are whole instants, and 1002 and 1003 are a
    /// duration and a period -- so there is no number to use, and occupying an unassigned one would
    /// leave documents that another decoder is entitled to reject.
    /// </remarks>
    public class TimeOnlyConverter : CborConverterBase<TimeOnly>
    {
        private const long TicksPerSecond = TimeSpan.TicksPerSecond;
        private const long SecondsPerDay = 24 * 60 * 60;

        private readonly CborOptions _options;

        public TimeOnlyConverter(CborOptions options)
        {
            _options = options;
        }

        public override TimeOnly Read(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.String:
                    ReadOnlySpan<byte> rawString = reader.ReadRawString();

                    if (!TryRead(rawString, out TimeOnly timeOnly))
                    {
                        throw reader.BuildException(
                            $"Invalid time format {Encoding.UTF8.GetString(rawString.ToArray())}");
                    }

                    return timeOnly;

                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    return FromSeconds(ref reader, reader.ReadInt64());

                case CborDataItemType.Double:
                case CborDataItemType.Single:
                    return FromSeconds(ref reader, reader.ReadDouble());

                default:
                    throw reader.BuildException("Invalid time format");
            }
        }

        public override void Write(ref CborWriter writer, TimeOnly value)
        {
            switch (_options.DateTimeFormat)
            {
                case DateTimeFormat.ISO8601:
                    // "FFFFFFF" rather than "fffffff": a whole second is written as "01:02:03", and
                    // the fraction appears only when there is one, at the width it needs.
                    writer.WriteString(value.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
                    break;

                // Whole seconds, so a value carrying a fraction does not round-trip through this
                // format. That matches what DateTimeFormat.Unix already does to a DateTime.
                case DateTimeFormat.Unix:
                    writer.WriteInt64(value.Ticks / TicksPerSecond);
                    break;

                case DateTimeFormat.UnixMilliseconds:
                    writer.WriteDouble((double)(value.Ticks / TimeSpan.TicksPerMillisecond) / 1000.0);
                    break;
            }
        }

        private static TimeOnly FromSeconds(ref CborReader reader, long seconds)
        {
            if (seconds < 0 || seconds >= SecondsPerDay)
            {
                throw reader.BuildException($"Time out of range: {seconds} seconds since midnight");
            }

            return new TimeOnly(seconds * TicksPerSecond);
        }

        private static TimeOnly FromSeconds(ref CborReader reader, double seconds)
        {
            // NaN fails every comparison, so it has to be refused by asking whether the value is in
            // range rather than whether it is out of it.
            if (!(seconds >= 0 && seconds < SecondsPerDay))
            {
                throw reader.BuildException($"Time out of range: {seconds} seconds since midnight");
            }

            return new TimeOnly((long)(seconds * TicksPerSecond));
        }

        /// <summary>
        /// RFC 3339 <c>partial-time</c>: <c>HH:mm:ss</c>, optionally followed by a fraction. A
        /// fraction finer than 100ns is truncated rather than refused, since a peer whose clock is
        /// more precise than <see cref="TimeOnly"/> is interoperating correctly.
        /// </summary>
        private static bool TryRead(ReadOnlySpan<byte> buffer, out TimeOnly value)
        {
            if (!TryReadInt32(ref buffer, 2, out int hours)
                || !TryReadByte(ref buffer, (byte)':')
                || !TryReadInt32(ref buffer, 2, out int minutes)
                || !TryReadByte(ref buffer, (byte)':')
                || !TryReadInt32(ref buffer, 2, out int seconds))
            {
                value = default;
                return false;
            }

            long ticks = 0;

            if (TryReadByte(ref buffer, (byte)'.'))
            {
                // A separator with no digit after it is malformed, so the first one is required.
                if (!TryReadInt32(ref buffer, 1, out int digit))
                {
                    value = default;
                    return false;
                }

                int places = 1;
                ticks = digit;

                while (TryReadInt32(ref buffer, 1, out digit))
                {
                    if (places < 7)
                    {
                        ticks = ticks * 10 + digit;
                        places++;
                    }
                }

                for (; places < 7; places++)
                {
                    ticks *= 10;
                }
            }

            // 24:00:00 is a legal RFC 3339 instant but not a legal time of day, and TimeOnly has no
            // room for a leap second either.
            if (!buffer.IsEmpty || hours > 23 || minutes > 59 || seconds > 59)
            {
                value = default;
                return false;
            }

            value = new TimeOnly(((hours * 60L + minutes) * 60L + seconds) * TicksPerSecond + ticks);
            return true;
        }

        private static bool TryReadInt32(ref ReadOnlySpan<byte> buffer, int digits, out int value)
        {
            if (buffer.Length < digits)
            {
                value = default;
                return false;
            }

            value = 0;

            for (int i = 0; i < digits; i++)
            {
                byte digit = buffer[i];

                if (digit < '0' || digit > '9')
                {
                    value = default;
                    return false;
                }

                value = value * 10 + (digit - '0');
            }

            buffer = buffer.Slice(digits);
            return true;
        }

        private static bool TryReadByte(ref ReadOnlySpan<byte> buffer, byte expected)
        {
            if (buffer.Length < 1 || buffer[0] != expected)
            {
                return false;
            }

            buffer = buffer.Slice(1);
            return true;
        }
    }
}
#endif
