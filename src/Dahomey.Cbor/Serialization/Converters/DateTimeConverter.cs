using System;
using System.Buffers.Text;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class DateTimeConverter : CborConverterBase<DateTime>
    {
        public override DateTime Read(ref CborReader reader)
        {
            switch(reader.GetCurrentDataItemType())
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

            if (!TryReadInt32(ref buffer, 2, out int hour))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)':'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int minute))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)':'))
            {
                value = default;
                return false;
            }

            if (!TryReadInt32(ref buffer, 2, out int second))
            {
                value = default;
                return false;
            }

            if (!TryReadByte(ref buffer, (byte)'Z'))
            {
                value = default;
                return false;
            }

            value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
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

        public override void Write(ref CborWriter writer, DateTime value)
        {
            switch (writer.Options.DateTimeFormat)
            {
                case DateTimeFormat.ISO8601:
                    writer.WriteString(value.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFK"));
                    break;

                case DateTimeFormat.Unix:
                    writer.WriteInt64(new DateTimeOffset(value).ToUnixTimeSeconds());
                    break;
            }
        }
    }
}
