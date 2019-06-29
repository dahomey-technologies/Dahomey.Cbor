using System;
using System.Globalization;
using System.Linq;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborNegative : CborValue, IComparable<CborNegative>, IEquatable<CborNegative>
    {
        private const int MinPreCreatedValue = -100;
        private const int MaxPreCreatedValue = -1;

        private static readonly CborNegative[] _preCreatedValues
            = Enumerable
                .Range(MinPreCreatedValue, MaxPreCreatedValue - MinPreCreatedValue + 1)
                .Select(i => new CborNegative(i))
                .ToArray();

        private long _value;

        public override CborValueType Type => CborValueType.Negative;

        private CborNegative(long value)
        {
            if (value >= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _value = value;
        }

        public override T Value<T>()
        {
            return (T)Convert.ChangeType(_value, typeof(T));
        }

        public void SetValue<T>(T value)
        {
            object convertedValue = Convert.ChangeType(value, typeof(double));
            if (convertedValue == null)
            {
                throw new NullReferenceException();
            }

            _value = (long)convertedValue;
        }

        public static implicit operator CborNegative(long value)
        {
            if (value >= MinPreCreatedValue && value <= MaxPreCreatedValue)
            {
                return _preCreatedValues[value - MinPreCreatedValue];
            }

            return new CborNegative(value);
        }

        public override string ToString()
        {
            return _value.ToString(CultureInfo.InvariantCulture);
        }

        public override int CompareTo(CborValue other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborNegative otherSigned)
            {
                return CompareTo(otherSigned);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborNegative other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.CompareTo(other._value);
        }

        public bool Equals(CborNegative other)
        {
            return other != null && _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborNegative value))
            {
                return false;
            }

            return _value == value._value;
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
