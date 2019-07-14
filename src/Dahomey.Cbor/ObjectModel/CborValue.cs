using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.ObjectModel
{
    public abstract class CborValue : IComparable<CborValue>, IEquatable<CborValue>
    {
        public abstract CborValueType Type { get; }

        public virtual T Value<T>()
        {
            throw new NotSupportedException(
                string.Format("type {0} not supported in {1}", typeof(T).Name, GetType().Name));
        }

        public static implicit operator CborValue(string value)
        {
            return value == null ? (CborValue)Null : (CborString)value;
        }

        public static implicit operator CborValue(char value)
        {
            return (CborString)value;
        }

        public static implicit operator CborValue(double value)
        {
            return (CborDouble)value;
        }

        public static implicit operator CborValue(float value)
        {
            return (CborSingle)value;
        }

        public static implicit operator CborValue(decimal value)
        {
            return (CborDouble)(double)value;
        }

        public static implicit operator CborValue(sbyte value)
        {
            return value >= 0 ? (CborValue)(CborPositive)(ulong)value : (CborNegative)(long)value;
        }

        public static implicit operator CborValue(byte value)
        {
            return (CborPositive)(ulong)value;
        }

        public static implicit operator CborValue(int value)
        {
            return value >= 0 ? (CborValue)(CborPositive)(ulong)value : (CborNegative)(long)value;
        }

        public static implicit operator CborValue(uint value)
        {
            return (CborPositive)(ulong)value;
        }

        public static implicit operator CborValue(short value)
        {
            return value >= 0 ? (CborValue)(CborPositive)(ulong)value : (CborNegative)(long)value;
        }

        public static implicit operator CborValue(ushort value)
        {
            return (CborPositive)(ulong)value;
        }

        public static implicit operator CborValue(long value)
        {
            return value >= 0 ? (CborValue)(CborPositive)(ulong)value : (CborNegative)value;
        }

        public static implicit operator CborValue(ulong value)
        {
            return (CborPositive)value;
        }

        public static implicit operator CborValue(bool value)
        {
            return (CborBoolean)value;
        }

        private static readonly CborNull s_Null = new CborNull();
        public static CborNull Null { get; } = s_Null;

        public static implicit operator CborValue(CborValue[] values)
        {
            return new CborArray(values);
        }

        public static implicit operator CborValue(Dictionary<CborValue, CborValue> pairs)
        {
            return pairs == null ? (CborValue)Null : new CborObject(pairs);
        }

        public static implicit operator CborValue(ReadOnlyMemory<byte> value)
        {
            return new CborByteString(value);
        }

        public static implicit operator CborValue(ReadOnlySpan<byte> value)
        {
            return new CborByteString(value.ToArray());
        }

        public bool Equals(CborValue other)
        {
            return Equals((object)other);
        }

        public int CompareTypeTo(CborValue other)
        {
            return other == null ? 1 : Type.CompareTo(other.Type);
        }

        public abstract int CompareTo(CborValue other);
        public abstract override bool Equals(object obj);
        public abstract override int GetHashCode();
    }
}
