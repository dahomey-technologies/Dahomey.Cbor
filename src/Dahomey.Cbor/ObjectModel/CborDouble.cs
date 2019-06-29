using System;
using System.Globalization;
using System.Linq;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborDouble : CborValue, IComparable<CborDouble>, IEquatable<CborDouble>
    {
        private const int MinPreCreatedValue = -100;
        private const int MaxPreCreatedValue = 100;

        private static readonly CborDouble[] _preCreatedValues
            = Enumerable
                .Range(MinPreCreatedValue, MaxPreCreatedValue - MinPreCreatedValue + 1)
                .Select(i => new CborDouble(i))
                .ToArray();

        private double _value;

        public override CborValueType Type { get { return CborValueType.Double; } }

        private CborDouble(double value)
        {
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

            _value = (double)convertedValue;
        }

        public static implicit operator CborDouble(double value)
        {
            int intValue = (int)value;
            if (intValue == value && intValue >= MinPreCreatedValue && intValue <= MaxPreCreatedValue)
            {
                return _preCreatedValues[intValue - MinPreCreatedValue];
            }

            return new CborDouble(value);
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

            if (other is CborDouble otherDouble)
            {
                return CompareTo(otherDouble);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborDouble other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.CompareTo(other._value);
        }

        public bool Equals(CborDouble other)
        {
            return other != null && _value.Equals(other._value);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborDouble value))
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
