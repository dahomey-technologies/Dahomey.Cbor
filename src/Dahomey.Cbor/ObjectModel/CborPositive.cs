using System;
using System.Globalization;
using System.Linq;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborPositive : CborValue, IComparable<CborPositive>, IEquatable<CborPositive>
    {
        private const int MinPreCreatedValue = 0;
        private const int MaxPreCreatedValue = 100;

        private static readonly CborPositive[] _preCreatedValues 
            = Enumerable
                .Range(MinPreCreatedValue, MaxPreCreatedValue - MinPreCreatedValue + 1)
                .Select(i => new CborPositive((ulong)i))
                .ToArray();

        private ulong _value;

        public override CborValueType Type { get { return CborValueType.Positive; } }

        private CborPositive(ulong value)
        {
            _value = value;
        }

        public override T Value<T>()
        {
            return Primitive<ulong, T>.Converter(_value);
        }

        public void SetValue<T>(T value)
        {
            _value = Primitive<T, ulong>.Converter(value);
        }

        public static implicit operator CborPositive(ulong value)
        {
            if (value >= MinPreCreatedValue && value <= MaxPreCreatedValue)
            {
                return _preCreatedValues[value - MinPreCreatedValue];
            }

            return new CborPositive(value);
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

            if (other is CborPositive otherUnsigned)
            {
                return CompareTo(otherUnsigned);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborPositive other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.CompareTo(other._value);
        }

        public bool Equals(CborPositive other)
        {
            return other != null && _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is CborPositive value))
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
