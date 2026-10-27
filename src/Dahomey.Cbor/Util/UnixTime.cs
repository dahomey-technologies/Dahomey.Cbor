using System;

namespace Dahomey.Cbor.Util
{
    internal static class UnixTime
    {
        /// <summary>
        /// Converts fractional seconds since the epoch, as tag 1 carries them, to ticks since the epoch.
        /// </summary>
        /// <remarks>
        /// A double near today's 1.7e9 seconds resolves only 2^-22 s, about 2.4 ticks, so the double
        /// <see cref="DateTimeFormat.UnixMilliseconds"/> writes for a millisecond is not that millisecond
        /// exactly, and <c>AddSeconds</c>, which truncates to a tick, read most of them back a tick or two
        /// off. A double that is the nearest one to a whole millisecond is read as that millisecond, the
        /// value any writer of milliseconds meant; anything else is rounded to the nearest tick.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Not a number, or far past either end of what a <see cref="DateTime"/> holds.
        /// </exception>
        public static long SecondsToTicks(double seconds)
        {
            // A DateTime ends about 2.5e11 seconds from the epoch; the bound keeps the casts below
            // defined. The comparison is false for NaN.
            if (!(Math.Abs(seconds) < 1e12))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            double milliseconds = Math.Round(seconds * 1000.0);

            if (milliseconds / 1000.0 == seconds)
            {
                return (long)milliseconds * TimeSpan.TicksPerMillisecond;
            }

            double wholeSeconds = Math.Floor(seconds);
            return (long)wholeSeconds * TimeSpan.TicksPerSecond
                + (long)Math.Round((seconds - wholeSeconds) * TimeSpan.TicksPerSecond);
        }
    }
}
