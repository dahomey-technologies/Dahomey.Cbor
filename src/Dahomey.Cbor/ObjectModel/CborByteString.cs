using System;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborByteString : CborValue, IComparable<CborByteString>, IEquatable<CborByteString>
    {
        private ReadOnlyMemory<byte> _value;

        public override CborValueType Type => CborValueType.ByteString;

        public CborByteString(ReadOnlyMemory<byte> value)
        {
            _value = value;
        }

        public override T Value<T>()
        {
            if (typeof(T) == typeof(ReadOnlyMemory<byte>))
            {
                return (T)(object)_value;
            }

            return base.Value<T>();
        }

        public static implicit operator CborByteString(ReadOnlyMemory<byte> value)
        {
            return new CborByteString(value);
        }

        public override int CompareTo(CborValue? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborByteString otherByteString)
            {
                return CompareTo(otherByteString);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborByteString? other)
        {
            if (other == null)
            {
                return 1;
            }

            return _value.Span.SequenceCompareTo(other._value.Span);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is CborByteString value))
            {
                return false;
            }

            return Equals(value);
        }

        public bool Equals(CborByteString? other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            return _value.Span.SequenceEqual(other._value.Span);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return $"h'{BitConverter.ToString(_value.Span.ToArray()).Replace("-", "")}'";
        }
    }
}
