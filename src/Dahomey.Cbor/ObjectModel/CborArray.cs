using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
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
        private readonly List<CborValue> _values;

        public int Count => _values.Count;
        public bool IsReadOnly => false;
        public int Capacity
        {
            get => _values.Capacity;
            set => _values.Capacity = value;
        }

        public CborArray()
        {
            _values = new List<CborValue>();
        }

        public CborArray(params CborValue[] values)
            : this((IEnumerable<CborValue>)values)
        {
        }

        public CborArray(IEnumerable<CborValue> values)
        {
            _values = new List<CborValue>(values.Select(v => v ?? CborValue.Null));
        }

        public static implicit operator CborArray(CborValue[] values)
        {
            return new CborArray(values);
        }

        public CborValue this[int index]
        {
            get { return _values[index]; }
            set { _values[index] = value; }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");

            bool separator = false;

            foreach (CborValue value in _values)
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
            _values.Add(item ?? Null);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public bool Contains(CborValue item)
        {
            return _values.Contains(item ?? Null);
        }

        public void CopyTo(CborValue[] array, int arrayIndex)
        {
            _values.CopyTo(array, arrayIndex);
        }

        public bool Remove(CborValue item)
        {
            return _values.Remove(item);
        }

        public IEnumerator<CborValue> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
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
            return _values.SequenceEqual(other._values);
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

            foreach (CborValue value in _values)
            {
                hash = 37 * hash + value.GetHashCode();
            }

            return hash;
        }

        public static CborArray FromCollection<T>(T collection, CborOptions options = null)
            where T : ICollection
        {
            options = options ?? CborOptions.Default;

            using (ByteBufferWriter buffer = new ByteBufferWriter())
            {
                ICborConverter listConverter = options.Registry.ConverterRegistry.Lookup<T>();
                CborWriter writer = new CborWriter(buffer);
                listConverter.Write(ref writer, collection);

                ICborConverter<CborArray> cborArrayConverter = options.Registry.ConverterRegistry.Lookup<CborArray>();
                CborReader reader = new CborReader(buffer.WrittenSpan);
                return cborArrayConverter.Read(ref reader);
            }
        }

        public T ToCollection<T>(CborOptions options = null) 
            where T : ICollection
        {
            options = options ?? CborOptions.Default;

            using (ByteBufferWriter buffer = new ByteBufferWriter())
            {
                ICborConverter<CborArray> cborArrayConverter = options.Registry.ConverterRegistry.Lookup<CborArray>();
                CborWriter writer = new CborWriter(buffer);
                cborArrayConverter.Write(ref writer, this);

                ICborConverter<T> objectConverter = options.Registry.ConverterRegistry.Lookup<T>();
                CborReader reader = new CborReader(buffer.WrittenSpan);
                return objectConverter.Read(ref reader);
            }
        }
    }
}
