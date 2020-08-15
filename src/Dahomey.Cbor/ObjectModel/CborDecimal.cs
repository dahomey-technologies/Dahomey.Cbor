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

        private decimal _value;

        public override CborValueType Type { get { return CborValueType.Decimal; } }

        private CborDecimal(decimal value)
        {
            _value = value;
        }

        public override T Value<T>()
        {
            return Primitive<decimal, T>.Converter(_value);
        }

        public void SetValue<T>(T value)
        {
            _value = Primitive<T, decimal>.Converter(value);
        }

        public static implicit operator CborDecimal(decimal value)
        {
            int intValue = (int)value;
            if (intValue == value && intValue >= MinPreCreatedValue && intValue <= MaxPreCreatedValue)
            {
                return _preCreatedValues[intValue - MinPreCreatedValue];
            }

            return new CborDecimal(value);
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
