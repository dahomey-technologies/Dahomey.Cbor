using System;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborString : CborValue, IComparable<CborString>, IEquatable<CborString>
    {
        private readonly static CborString _empty = new CborString(string.Empty);

        private string _value;

        public override CborValueType Type { get { return CborValueType.String; } }

        private CborString(string value)
        {
            _value = value ?? throw new ArgumentOutOfRangeException(nameof(value));
        }

        public override T Value<T>()
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)_value;
            }

            return base.Value<T>();
        }

        public void SetValue(string value)
        {
            _value = value;
        }

        public static implicit operator CborString(string value)
        {
            if (value == string.Empty)
            {
                return _empty;
            }

            return new CborString(value);
        }

        public static implicit operator CborString(char value)
        {
            return new CborString(new string(value, 1));
        }

        public override string ToString()
        {
            return string.Format("\"{0}\"", _value);
        }

        public override int CompareTo(CborValue other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborString otherString)
            {
                return CompareTo(otherString);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborString other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.CompareTo(other._value);
        }

        public bool Equals(CborString other)
        {
            return other != null && _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborString value))
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
