using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborSingle : CborValue, IComparable<CborSingle>, IEquatable<CborSingle>
    {
        private const int MinPreCreatedValue = -100;
        private const int MaxPreCreatedValue = 100;

        private static readonly CborSingle[] _preCreatedValues
            = Enumerable
                .Range(MinPreCreatedValue, MaxPreCreatedValue - MinPreCreatedValue + 1)
                .Select(i => new CborSingle(i))
                .ToArray();

        private float _value;

        public override CborValueType Type { get { return CborValueType.Single; } }

        private CborSingle(float value)
        {
            _value = value;
        }

        public override T Value<T>()
        {
            return Primitive<float, T>.Converter(_value);
        }

        public void SetValue<T>(T value)
        {
            _value = Primitive<T, float>.Converter(value);
        }

        public static implicit operator CborSingle(float value)
        {
            int intValue = (int)value;
            if (intValue == value && intValue >= MinPreCreatedValue && intValue <= MaxPreCreatedValue)
            {
                return _preCreatedValues[intValue - MinPreCreatedValue];
            }

            return new CborSingle(value);
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

            if (other is CborSingle otherSingle)
            {
                return CompareTo(otherSingle);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborSingle other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.CompareTo(other._value);
        }

        public bool Equals(CborSingle other)
        {
            return other != null && _value.Equals(other._value);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborSingle value))
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
