using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborDecimal : CborValue, IComparable<CborDecimal>, IEquatable<CborDecimal>
    {
        private const int MinPreCreatedValue = -100;
        private const int MaxPreCreatedValue = 100;

        private static readonly CborDecimal[] _preCreatedValues
            = Enumerable
                .Range(MinPreCreatedValue, MaxPreCreatedValue - MinPreCreatedValue + 1)
                .Select(i => new CborDecimal(i))
                .ToArray();

        private readonly decimal _value;

        public override CborValueType Type { get { return CborValueType.Decimal; } }

        private CborDecimal(decimal value)
        {
            _value = value;
        }

        /// <summary>
        /// Copies before tagging: CborDecimal hands out shared instances for common values, so
        /// tagging in place would attach the tag to every occurrence of that value in the
        /// process. See <see cref="CborValue.WithSemanticTag"/>.
        /// </summary>
        internal override CborValue WithSemanticTag(ulong semanticTag)
        {
            return new CborDecimal(_value) { SemanticTag = semanticTag };
        }

        public override T Value<T>()
        {
            return Primitive<decimal, T>.Converter(_value);
        }


        public static implicit operator CborDecimal(decimal value)
        {
            // The range is checked first: casting a decimal outside Int32 to int throws.
            if (value >= MinPreCreatedValue && value <= MaxPreCreatedValue && IsPlainInteger(value))
            {
                return _preCreatedValues[(int)value - MinPreCreatedValue];
            }

            return new CborDecimal(value);
        }

        /// <summary>
        /// Whether a pre-created instance represents the value exactly: an integer with no scale
        /// (1.0m keeps its trailing zero when written) and not negative zero.
        /// </summary>
        private static bool IsPlainInteger(decimal value)
        {
#if NETSTANDARD2_0
            int flags = decimal.GetBits(value)[3];
#else
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);
            int flags = bits[3];
#endif
            // The flags hold the scale in bits 16 to 23 and the sign in bit 31.
            return value != 0 ? (flags & 0x00FF0000) == 0 : flags == 0;
        }

        public override string ToString()
        {
            return _value.ToString(CultureInfo.InvariantCulture);
        }

        public override int CompareTo(CborValue? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborDecimal otherDecimal)
            {
                return CompareTo(otherDecimal);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborDecimal? other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.CompareTo(other._value);
        }

        public bool Equals(CborDecimal? other)
        {
            return other != null && _value.Equals(other._value);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is CborDecimal value))
            {
                return false;
            }

            return _value.Equals(value._value);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = 37 * hash + Type.GetHashCode();
            hash = 37 * hash + _value.GetHashCode();
            return hash;
        }
    }
}
