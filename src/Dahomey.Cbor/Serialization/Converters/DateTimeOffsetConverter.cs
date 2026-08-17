using System;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="DateTimeOffset"/>, keeping the offset it carries.
    /// </summary>
    /// <remarks>
    /// Only the tag 0 form can hold an offset. RFC 8949 tag 1 is a count of seconds since the epoch,
    /// which names an instant and has nowhere to put the offset the value was observed at, so
    /// <see cref="DateTimeFormat.Unix"/> and <see cref="DateTimeFormat.UnixMilliseconds"/> write the
    /// correct instant with the offset dropped and read back at <see cref="TimeSpan.Zero"/>. That is a
    /// property of the encoding rather than of this converter: a caller who needs the offset preserved
    /// has to leave <see cref="CborOptions.DateTimeFormat"/> at <see cref="DateTimeFormat.ISO8601"/>.
    /// </remarks>
    public class DateTimeOffsetConverter : CborConverterBase<DateTimeOffset>
    {
        /// <summary>RFC 8949's own bound on a numeric offset, which DateTimeOffset shares.</summary>
        private const int MaxOffsetMinutes = 14 * 60;

        private readonly CborOptions _options;

        public DateTimeOffsetConverter(CborOptions options)
        {
            _options = options;
        }

        public override DateTimeOffset Read(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.String:
                    ReadOnlySpan<byte> rawString = reader.ReadRawString();

                    if (!TryRead(rawString, out DateTimeOffset dateTimeOffset))
                    {
                        throw reader.BuildException(
                            $"Invalid date format {Encoding.UTF8.GetString(rawString.ToArray())}");
                    }

                    return dateTimeOffset;

                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    return DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64());

                case CborDataItemType.Double:
                case CborDataItemType.Single:
                    return DateTimeOffset.FromUnixTimeSeconds(0).AddSeconds(reader.ReadDouble());

                default:
                    throw reader.BuildException("Invalid date format");
            }
        }

        public override void Write(ref CborWriter writer, DateTimeOffset value)
        {
            switch (_options.DateTimeFormat)
            {
                case DateTimeFormat.ISO8601:
                    writer.WriteSemanticTag(0);
                    // "K" on a DateTimeOffset is the numeric offset, so a zero offset writes as
                    // "+00:00" rather than the "Z" a UTC DateTime produces. Both are RFC 3339 and both
                    // read back here as the same instant at the same offset.
                    writer.WriteString(
                        value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture));
                    break;

                case DateTimeFormat.Unix:
                    writer.WriteSemanticTag(1);
                    writer.WriteInt64(value.ToUnixTimeSeconds());
                    break;

                case DateTimeFormat.UnixMilliseconds:
                    writer.WriteSemanticTag(1);
                    writer.WriteDouble((double)value.ToUnixTimeMilliseconds() / 1000.0);
                    break;
            }
        }

        /// <summary>
        /// RFC 3339 <c>date-time</c>. Unlike <see cref="DateTimeConverter"/>, which converts an offset
        /// away to reach UTC because <see cref="DateTime"/> has nowhere to keep it, the offset here is
        /// the part worth keeping and the local time is stored beside it unmodified.
        /// </summary>
        private bool TryRead(ReadOnlySpan<byte> buffer, out DateTimeOffset value)
        {
            if (!TryReadInt32(ref buffer, 4, out int year)
                || !TryReadByte(ref buffer, (byte)'-')
                || !TryReadInt32(ref buffer, 2, out int month)
                || !TryReadByte(ref buffer, (byte)'-')
                || !TryReadInt32(ref buffer, 2, out int day)
                || !TryReadByte(ref buffer, (byte)'T')
                || !TryReadInt32(ref buffer, 2, out int hours)
                || !TryReadByte(ref buffer, (byte)':')
                || !TryReadInt32(ref buffer, 2, out int minutes)
                || !TryReadByte(ref buffer, (byte)':')
                || !TryReadInt32(ref buffer, 2, out int seconds))
            {
                value = default;
                return false;
            }

            int milliseconds = 0;

            if (TryReadByte(ref buffer, (byte)'.'))
            {
                if (!TryReadInt32(ref buffer, 1, out int digit))
                {
                    value = default;
                    return false;
                }

                int places = 1;
                milliseconds = digit;

                while (TryReadInt32(ref buffer, 1, out digit))
                {
                    // Past three places the digits are finer than the milliseconds this reads, and are
                    // dropped rather than refused -- a peer with a more precise clock is interoperating
                    // correctly. Matches what DateTimeConverter accepts.
                    if (places < 3)
                    {
                        milliseconds = milliseconds * 10 + digit;
                        places++;
                    }
                }

                for (; places < 3; places++)
                {
                    milliseconds *= 10;
                }
            }

            if (!TryReadOffset(ref buffer, out TimeSpan? offset) || !buffer.IsEmpty)
            {
                value = default;
                return false;
            }

            try
            {
                // An unqualified string names no offset, so the kind the options ask for decides one:
                // Utc gives zero and Local the machine's, exactly as constructing a DateTimeOffset from
                // a DateTime of that kind does.
                if (offset is null)
                {
                    value = new DateTimeOffset(
                        new DateTime(
                            year, month, day, hours, minutes, seconds, milliseconds,
                            _options.UnqualifiedTimeZoneDateTimeKind));

                    return true;
                }

                value = new DateTimeOffset(
                    year, month, day, hours, minutes, seconds, milliseconds, offset.Value);

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // A component the calendar refuses -- month 13, February 30 -- or a local time the
                // offset pushes outside DateTimeOffset's range. Refused as a format error rather than
                // escaping as an exception a caller's catch(CborException) would miss.
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Reads the RFC 3339 <c>time-offset</c>, returning a null offset for a string that carries
        /// none. <c>Z</c> is an offset -- zero, stated -- and is not the same as its absence.
        /// </summary>
        private static bool TryReadOffset(ref ReadOnlySpan<byte> buffer, out TimeSpan? offset)
        {
            offset = null;

            if (buffer.IsEmpty)
            {
                return true;
            }

            if (TryReadByte(ref buffer, (byte)'Z'))
            {
                offset = TimeSpan.Zero;
                return true;
            }

            bool negative;

            if (TryReadByte(ref buffer, (byte)'-'))
            {
                negative = true;
            }
            else if (TryReadByte(ref buffer, (byte)'+'))
            {
                negative = false;
            }
            else
            {
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int offsetHours)
                || !TryReadByte(ref buffer, (byte)':')
                || !TryReadInt32(ref buffer, 2, out int offsetMinutes))
            {
                return false;
            }

            int total = offsetHours * 60 + offsetMinutes;

            // DateTimeOffset admits whole minutes within 14 hours, so anything wider is refused here
            // rather than thrown from the constructor.
            if (offsetMinutes > 59 || total > MaxOffsetMinutes)
            {
                return false;
            }

            offset = TimeSpan.FromMinutes(negative ? -total : total);
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
