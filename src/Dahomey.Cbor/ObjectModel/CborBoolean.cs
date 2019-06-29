using System;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborBoolean : CborValue, IComparable<CborBoolean>, IEquatable<CborBoolean>
    {
        private readonly static CborBoolean _false = new CborBoolean(false);
        private readonly static CborBoolean _true = new CborBoolean(true);

        private bool _value;

        public override CborValueType Type { get { return CborValueType.Boolean; } }

        private CborBoolean(bool value)
        {
            _value = value;
        }

        public override T Value<T>()
        {
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)_value;
            }

            return base.Value<T>();
        }

        public void SetValue(bool value)
        {
            _value = value;
        }

        public static implicit operator CborBoolean(bool value)
        {
            return value ? _true : _false;
        }

        public override string ToString()
        {
            return _value ? "true" : "false";
        }

        public override int CompareTo(CborValue other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborBoolean otherBoolean)
            {
                return CompareTo(otherBoolean);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborBoolean other)
        {
            if (other == null)
            {
                return 1;
            }

            return (_value ? 1 : 0).CompareTo(other._value ? 1 : 0);
        }

        public bool Equals(CborBoolean other)
        {
            return other != null && _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborBoolean value))
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
