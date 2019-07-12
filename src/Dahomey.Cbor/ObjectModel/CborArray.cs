using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborArray : CborValue, ICollection<CborValue>, IComparable<CborArray>, IEquatable<CborArray>
    {
        public override CborValueType Type { get { return CborValueType.Array; } }
        public List<CborValue> Values { get; private set; }

        public int Count => Values.Count;
        public bool IsReadOnly => ((ICollection<CborValue>)Values).IsReadOnly;
        public int Capacity
        {
            get => Values.Capacity;
            set => Values.Capacity = value;
        }

        public CborArray()
        {
            Values = new List<CborValue>();
        }

        public CborArray(params CborValue[] values)
            : this((IEnumerable<CborValue>)values)
        {
        }

        public CborArray(IEnumerable<CborValue> values)
        {
            Values = new List<CborValue>(values.Select(v => v ?? CborValue.Null));
        }

        public static implicit operator CborArray(CborValue[] values)
        {
            return new CborArray(values);
        }

        public CborValue this[int index]
        {
            get { return Values[index]; }
            set { Values[index] = value; }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");

            bool separator = false;

            foreach (CborValue value in Values)
            {
                if (separator)
                {
                    sb.Append(",");
                }

                separator = true;

                sb.Append(value ?? Null);
            }

            sb.Append("]");

            return sb.ToString();
        }

        public void Add(CborValue item)
        {
            Values.Add(item ?? Null);
        }

        public void Clear()
        {
            Values.Clear();
        }

        public bool Contains(CborValue item)
        {
            return Values.Contains(item ?? Null);
        }

        public void CopyTo(CborValue[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(CborValue item)
        {
            return Values.Remove(item);
        }

        public IEnumerator<CborValue> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public override int CompareTo(CborValue other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborArray otherArray)
            {
                return CompareTo(otherArray);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborArray other)
        {
            if (other == null)
            {
                return 1;
            }

            using (var enumerator = GetEnumerator())
            using (var otherEnumerator = other.GetEnumerator())
            {
                while (true)
                {
                    var hasNext = enumerator.MoveNext();
                    var otherHasNext = otherEnumerator.MoveNext();
                    if (!hasNext && !otherHasNext) { return 0; }
                    if (!hasNext) { return -1; }
                    if (!otherHasNext) { return 1; }

                    var value = enumerator.Current;
                    var otherValue = otherEnumerator.Current;
                    var result = value.CompareTo(otherValue);
                    if (result != 0) { return result; }
                }
            }
        }

        public bool Equals(CborArray other)
        {
            return Values.SequenceEqual(other.Values);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborArray value))
            {
                return false;
            }

            return Equals(value);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = 37 * hash + Type.GetHashCode();

            foreach (CborValue value in Values)
            {
                hash = 37 * hash + value.GetHashCode();
            }

            return hash;
        }
    }
}
