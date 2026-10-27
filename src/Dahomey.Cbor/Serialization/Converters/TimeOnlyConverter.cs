#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="TimeOnly"/> as an RFC 3339 <c>partial-time</c> string.
    /// </summary>
    /// <remarks>
    /// Deliberately untagged, unlike <see cref="DateOnlyConverter"/>. The CBOR tag registry has
    /// nothing for a time of day -- tags 0, 1, 4 and 5 are whole instants, and 1002 and 1003 are a
    /// duration and a period -- so there is no number to use, and occupying an unassigned one would
    /// leave documents that another decoder is entitled to reject.
    /// <para>
    /// There is one encoding whatever <see cref="CborOptions.DateTimeFormat"/> says: that option
    /// chooses between representations of an instant, and a time of day is not one.
    /// </para>
    /// <para>
    /// Also read, never written: the map earlier versions wrote for a <see cref="TimeOnly"/> before it
    /// had a converter. <see cref="CborOptions.DuplicateKeyMode"/> governs a repeated key in it.
    /// </para>
    /// </remarks>
    public class TimeOnlyConverter : CborConverterBase<TimeOnly>
    {
        private const long TicksPerSecond = TimeSpan.TicksPerSecond;

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

                case CborDataItemType.Map:
                    return ReadLegacyMap(ref reader);

                default:
                    throw reader.BuildException("Invalid time format");
            }
        }

        public override void Write(ref CborWriter writer, TimeOnly value)
        {
            // "FFFFFFF" rather than "fffffff": a whole second is written as "01:02:03", and the
            // fraction appears only when there is one, at the width it needs.
            writer.WriteString(value.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The map earlier versions wrote for a <see cref="TimeOnly"/>, an object of its public
        /// properties. <c>Ticks</c> carries the whole value, seconds and fraction included, so it is
        /// authoritative; <c>Hour</c>, <c>Minute</c> and the finer members are the fallback.
        /// </summary>
        private TimeOnly ReadLegacyMap(ref CborReader reader)
        {
            Dictionary<string, long> members = LegacyDateTimeMap.Read(ref reader, _options);

            if (members.TryGetValue("Ticks", out long ticks))
            {
                if (ticks >= 0 && ticks < TimeSpan.TicksPerDay)
                {
                    return new TimeOnly(ticks);
                }
            }
            else if (members.TryGetValue("Hour", out long hour)
                && members.TryGetValue("Minute", out long minute))
            {
                members.TryGetValue("Second", out long second);
                members.TryGetValue("Millisecond", out long millisecond);
                members.TryGetValue("Microsecond", out long microsecond);

                if (hour >= 0 && hour < 24 && minute >= 0 && minute < 60
                    && second >= 0 && second < 60 && millisecond >= 0 && millisecond < 1000
                    && microsecond >= 0 && microsecond < 1000)
                {
                    return new TimeOnly(
                        (((hour * 60 + minute) * 60 + second) * 1000 + millisecond) * TimeSpan.TicksPerMillisecond
                        + microsecond * (TimeSpan.TicksPerMillisecond / 1000));
                }
            }

            throw reader.BuildException("Invalid time format");
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
