#if NET6_0_OR_GREATER
using System;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="DateOnly"/> using the two encodings RFC 8943 registers for a
    /// date with no time of day: tag 1004 over an RFC 3339 <c>full-date</c> string, and tag 100 over
    /// the number of days since 1970-01-01.
    /// </summary>
    /// <remarks>
    /// Which one is written follows <see cref="CborOptions.DateTimeFormat"/>, so a document keeps one
    /// shape throughout rather than encoding its dates differently from its timestamps. Both are read
    /// whichever is configured, since that setting describes what this end emits and says nothing
    /// about what a peer sent.
    /// </remarks>
    public class DateOnlyConverter : CborConverterBase<DateOnly>
    {
        /// <summary>RFC 8943's tag for an RFC 3339 <c>full-date</c> string.</summary>
        public const ulong FullDateTag = 1004;

        /// <summary>RFC 8943's tag for a count of days since 1970-01-01.</summary>
        public const ulong EpochDayTag = 100;

        private static readonly DateOnly _epoch = new DateOnly(1970, 1, 1);

        private readonly CborOptions _options;

        public DateOnlyConverter(CborOptions options)
        {
            _options = options;
        }

        public override DateOnly Read(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.String:
                    ReadOnlySpan<byte> rawString = reader.ReadRawString();

                    if (!TryRead(rawString, out DateOnly dateOnly))
                    {
                        throw reader.BuildException(
                            $"Invalid date format {Encoding.UTF8.GetString(rawString.ToArray())}");
                    }

                    return dateOnly;

                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    return ReadEpochDay(ref reader);

                default:
                    throw reader.BuildException("Invalid date format");
            }
        }

        public override void Write(ref CborWriter writer, DateOnly value)
        {
            switch (_options.DateTimeFormat)
            {
                case DateTimeFormat.ISO8601:
                    writer.WriteSemanticTag(FullDateTag);
                    writer.WriteString(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    break;

                // A date has no time of day to carry milliseconds, so both numeric formats are the
                // same count of whole days rather than one of them being a finer-grained variant.
                case DateTimeFormat.Unix:
                case DateTimeFormat.UnixMilliseconds:
                    writer.WriteSemanticTag(EpochDayTag);
                    writer.WriteInt32(value.DayNumber - _epoch.DayNumber);
                    break;
            }
        }

        /// <summary>
        /// A day count is bounded by <see cref="DateOnly"/>'s own range, which is far narrower than
        /// the integer that carries it, so a hostile or merely mistaken document is refused rather
        /// than throwing <see cref="ArgumentOutOfRangeException"/> from the arithmetic.
        /// </summary>
        private DateOnly ReadEpochDay(ref CborReader reader)
        {
            long days = reader.ReadInt64();

            long dayNumber = days + _epoch.DayNumber;

            if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
            {
                throw reader.BuildException($"Date out of range: {days} days since the epoch");
            }

            return DateOnly.FromDayNumber((int)dayNumber);
        }

        /// <summary>
        /// RFC 3339 <c>full-date</c>, which is exactly ten characters and admits no alternatives --
        /// no shortened year, no omitted separator, no trailing time.
        /// </summary>
        private static bool TryRead(ReadOnlySpan<byte> buffer, out DateOnly value)
        {
            if (buffer.Length != 10
                || !TryReadInt32(ref buffer, 4, out int year)
                || !TryReadByte(ref buffer, (byte)'-')
                || !TryReadInt32(ref buffer, 2, out int month)
                || !TryReadByte(ref buffer, (byte)'-')
                || !TryReadInt32(ref buffer, 2, out int day))
            {
                value = default;
                return false;
            }

            // Guarding the components keeps a malformed date a CborException rather than the
            // ArgumentOutOfRangeException the constructor would raise, and February's length makes
            // the day check unavoidable in any case.
            if (year < 1 || year > 9999
                || month < 1 || month > 12
                || day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                value = default;
                return false;
            }

            value = new DateOnly(year, month, day);
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
