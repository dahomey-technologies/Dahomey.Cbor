using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Util
{
    /// <summary>
    /// Decides whether a member holds its default value, for <c>[CborIgnoreIfDefault]</c>.
    /// </summary>
    /// <remarks>
    /// An omitted member reads back as the default itself, so a value may be omitted only when it is
    /// indistinguishable from the default, and equality is weaker than that for the types here: -0.0
    /// equals 0.0, 0.00m equals 0m, two <see cref="DateTime"/>s of different kinds compare their ticks
    /// alone and two <see cref="DateTimeOffset"/>s their instants alone, yet each of those is written,
    /// and read back, differently. These types are compared on everything they carry; any other type on
    /// its own equality.
    /// </remarks>
    internal static class DefaultValueComparer<T>
    {
        public static readonly Func<T, T, bool> AreIdentical =
            (Func<T, T, bool>?)DefaultValueComparer.Create(typeof(T)) ?? EqualityComparer<T>.Default.Equals;
    }

    internal static class DefaultValueComparer
    {
        public static Delegate? Create(Type type)
        {
            if (type == typeof(double)) return (Func<double, double, bool>)SameDouble;
            if (type == typeof(double?)) return Lift<double>(SameDouble);
            if (type == typeof(float)) return (Func<float, float, bool>)SameSingle;
            if (type == typeof(float?)) return Lift<float>(SameSingle);
            if (type == typeof(Half)) return (Func<Half, Half, bool>)SameHalf;
            if (type == typeof(Half?)) return Lift<Half>(SameHalf);
            if (type == typeof(decimal)) return (Func<decimal, decimal, bool>)SameDecimal;
            if (type == typeof(decimal?)) return Lift<decimal>(SameDecimal);
            if (type == typeof(DateTime)) return (Func<DateTime, DateTime, bool>)SameDateTime;
            if (type == typeof(DateTime?)) return Lift<DateTime>(SameDateTime);
            if (type == typeof(DateTimeOffset)) return (Func<DateTimeOffset, DateTimeOffset, bool>)SameDateTimeOffset;
            if (type == typeof(DateTimeOffset?)) return Lift<DateTimeOffset>(SameDateTimeOffset);
            return null;
        }

        // Bit for bit, which tells the zeros apart, and NaN payloads, which the writer keeps.
        private static bool SameDouble(double x, double y)
            => BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y);

        private static bool SameSingle(float x, float y)
            => HalfHelpers.SingleToInt32Bits(x) == HalfHelpers.SingleToInt32Bits(y);

        private static bool SameHalf(Half x, Half y)
            => HalfHelpers.HalfToUInt16Bits(x) == HalfHelpers.HalfToUInt16Bits(y);

        // Equal decimals differ only in scale and sign, both in the flags word; it is read only when
        // the values are equal, which spares the allocation on netstandard2.0 most of the time.
        private static bool SameDecimal(decimal x, decimal y)
            => x == y && decimal.GetBits(x)[3] == decimal.GetBits(y)[3];

        private static bool SameDateTime(DateTime x, DateTime y)
            => x.Ticks == y.Ticks && x.Kind == y.Kind;

        private static bool SameDateTimeOffset(DateTimeOffset x, DateTimeOffset y)
            => x.EqualsExact(y);

        private static Func<TV?, TV?, bool> Lift<TV>(Func<TV, TV, bool> areIdentical) where TV : struct
            => (x, y) => x.HasValue ? y.HasValue && areIdentical(x.Value, y.Value) : !y.HasValue;
    }
}
