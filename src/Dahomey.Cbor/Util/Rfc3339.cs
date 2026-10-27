using System;

namespace Dahomey.Cbor.Util
{
    /// <summary>
    /// The parts of RFC 3339 section 5.6 that the date and time converters share, so that a
    /// <see cref="DateTime"/> and a <see cref="DateTimeOffset"/> read the same text as the same instant.
    /// </summary>
    internal static class Rfc3339
    {
        /// <summary>
        /// Reads an optional <c>time-secfrac = "." 1*DIGIT</c>. It is a decimal fraction of a second, so
        /// each digit is worth a tenth of the one before it. A tick is 100 ns, which is the first seven
        /// digits; any further ones are consumed and truncated, never rounded, so the fraction cannot
        /// carry into the next second.
        /// </summary>
        /// <param name="buffer">Positioned at the character after the seconds; advanced past the fraction.</param>
        /// <param name="ticks">The fraction as a count of 100 ns ticks, below one second.</param>
        /// <returns>
        /// True when there is no fraction at all, or a well-formed one. False for a <c>.</c> followed by
        /// no digit.
        /// </returns>
        public static bool TryReadSecondFraction(ref ReadOnlySpan<byte> buffer, out long ticks)
        {
            ticks = 0;

            if (buffer.IsEmpty || buffer[0] != (byte)'.')
            {
                return true;
            }

            buffer = buffer.Slice(1);

            int fractionDigits = 0;
            long scale = 1_000_000;

            while (!buffer.IsEmpty && buffer[0] >= (byte)'0' && buffer[0] <= (byte)'9')
            {
                if (fractionDigits < 7)
                {
                    ticks += (buffer[0] - (byte)'0') * scale;
                    scale /= 10;
                }

                fractionDigits++;
                buffer = buffer.Slice(1);
            }

            return fractionDigits != 0;
        }
    }
}
