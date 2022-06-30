using System;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class DateTimeConverter : CborConverterBase<DateTime>
    {
        private readonly CborOptions _options;

        public DateTimeConverter(CborOptions options)
        {
            _options = options;
        }

        public override DateTime Read(ref CborReader reader)
        {
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.String:
                    ReadOnlySpan<byte> rawString = reader.ReadRawString();
                    if (!TryRead(rawString, out DateTime dateTime))
                    {
                        throw reader.BuildException($"Invalid date format {Encoding.UTF8.GetString(rawString)}");
                    }
                    return dateTime;

                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    long unixTime = reader.ReadInt64();
                    return DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;

                case CborDataItemType.Double:
                case CborDataItemType.Single:
                    double unixTimeDouble = reader.ReadDouble();
                    return DateTimeOffset.FromUnixTimeSeconds(0).DateTime.AddSeconds(unixTimeDouble);

                default:
                    throw reader.BuildException($"Invalid date format");
            }
        }

        private bool TryRead(ReadOnlySpan<byte> buffer, out DateTime value)
        {
            if (!TryReadInt32(ref buffer, 4, out int year))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)'-'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int month))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)'-'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int day))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)'T'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int hours))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)':'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int minutes))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)':'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int seconds))
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

                milliseconds = digit;

                while (TryReadInt32(ref buffer, 1, out digit))
                {
                    milliseconds = milliseconds * 10 + digit;
                }
            }

            // unspecified time zone => assume local
            if (buffer.IsEmpty)
            {
                value = new DateTime(year, month, day, hours, minutes, seconds, milliseconds, DateTimeKind.Local);
            }
            // UTC
            else if (TryReadByte(ref buffer, (byte)'Z'))
            {
                value = new DateTime(year, month, day, hours, minutes, seconds, milliseconds, DateTimeKind.Utc);
            }
            // Other time zones => convert to local
            else
            {
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
                    value = default;
                    return false;
                }

                if (!TryReadInt32(ref buffer, 2, out int offsetHours))
                {
                    value = default;
                    return false;
                }

                if (!TryReadByte(ref buffer, (byte)':'))
                {
                    value = default;
                    return false;
                }

                if (!TryReadInt32(ref buffer, 2, out int offsetMinutes))
                {
                    value = default;
                    return false;
                }

                if (negative)
                {
                    offsetHours = -offsetHours;
                    offsetMinutes = -offsetMinutes;
                }

                DateTimeOffset offset = new DateTimeOffset(
                    year, month, day, hours, minutes, seconds, milliseconds,
                    TimeSpan.FromHours(offsetHours) + TimeSpan.FromMinutes(offsetMinutes));

                value = offset.LocalDateTime;
            }

            return true;
        }

        private bool TryReadInt32(ref ReadOnlySpan<byte> buffer, int digits, out int value)
        {
            if (buffer.Length < digits)
            {
                value = default;
                return false;
            }

            if (!Utf8Parser.TryParse(buffer.Slice(0, digits), out value, out int bytesConsumed))
            {
                value = default;
                return false;
            }

            buffer = buffer.Slice(bytesConsumed);
            return true;
        }

        private bool TryReadByte(ref ReadOnlySpan<byte> buffer, byte expectedValue)
        {
            if (buffer.IsEmpty)
            {
                return false;
            }

            if (buffer[0] != expectedValue)
            {
                return false;
            }

            buffer = buffer.Slice(1);
            return true;
        }

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7049#section-2.4.1
        /// </summary>
        public override void Write(ref CborWriter writer, DateTime value)
        {
            switch (_options.DateTimeFormat)
            {
                case DateTimeFormat.ISO8601:
                    writer.WriteSemanticTag(0);
                    writer.WriteString(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFK", CultureInfo.InvariantCulture));
                    break;

                case DateTimeFormat.Unix:
                    writer.WriteSemanticTag(1);
                    writer.WriteInt64(new DateTimeOffset(value).ToUnixTimeSeconds());
                    break;

                case DateTimeFormat.UnixMilliseconds:
                    writer.WriteSemanticTag(1);
                    writer.WriteDouble((double)new DateTimeOffset(value).ToUnixTimeMilliseconds() / 1000.0);
                    break;
            }
        }
    }
}
