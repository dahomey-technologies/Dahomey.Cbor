using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Text;
using CborPair = System.Collections.Generic.KeyValuePair<Dahomey.Cbor.ObjectModel.CborValue, Dahomey.Cbor.ObjectModel.CborValue>;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborObject : 
        CborValue, 
        IComparable<CborObject>, 
        IEquatable<CborObject>,
        IDictionary<CborValue, CborValue>
    {
        private readonly Dictionary<CborValue, CborValue> _pairs;

        public override CborValueType Type => CborValueType.Object;
        public ICollection<CborValue> Keys => _pairs.Keys;
        public ICollection<CborValue> Values => _pairs.Values;
        public int Count => _pairs.Count;
        public bool IsReadOnly => false;

        public CborObject()
        {
            _pairs = new Dictionary<CborValue, CborValue>();
        }

        public CborObject(IDictionary<CborValue, CborValue> pairs)
        {
            _pairs = new Dictionary<CborValue, CborValue>(pairs);
        }

        public static implicit operator CborObject(Dictionary<CborValue, CborValue> pairs)
        {
            return new CborObject(pairs);
        }

        public CborValue this[CborValue name]
        {
            get => _pairs[name];
            set => _pairs[name] = value ?? Null;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("{");

            bool separator = false;

            foreach (CborPair pair in _pairs)
            {
                if (separator)
                {
                    sb.Append(",");
                }

                separator = true;

                sb.AppendFormat("{0}:{1}", pair.Key, pair.Value);
            }

            sb.Append("}");

            return sb.ToString();
        }

        public override int CompareTo(CborValue other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborObject otherObject)
            {
                return CompareTo(otherObject);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborObject other)
        {
            if (other == null)
            {
                return 1;
            }

            using var enumerator = _pairs.GetEnumerator();
            using var otherEnumerator = other._pairs.GetEnumerator();
            while (true)
            {
                bool hasNext = enumerator.MoveNext();
                bool otherHasNext = otherEnumerator.MoveNext();
                if (!hasNext && !otherHasNext) { return 0; }
                if (!hasNext) { return -1; }
                if (!otherHasNext) { return 1; }

                CborPair pair = enumerator.Current;
                CborPair otherPair = otherEnumerator.Current;
                int result = pair.Key.CompareTo(otherPair.Key);
                if (result != 0) { return result; }
                result = pair.Value.CompareTo(otherPair.Value);
                if (result != 0) { return result; }
            }
        }

        public bool Equals(CborObject other)
        {
            return other != null && _pairs.SequenceEqual(other._pairs);
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is CborObject value))
            {
                return false;
            }

            return Equals(value);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = 37 * hash + Type.GetHashCode();

            foreach(CborPair pair in _pairs)
            {
                hash = 37 * hash + pair.Key.GetHashCode();
                hash = 37 * hash + pair.Value.GetHashCode();
            }

            return hash;
        }

        public void Add(CborValue key, CborValue value)
        {
            _pairs.Add(key, value ?? Null);
        }

        public bool ContainsKey(CborValue key)
        {
            return _pairs.ContainsKey(key);
        }

        public bool Remove(CborValue key)
        {
            return _pairs.Remove(key);
        }

        public bool TryGetValue(CborValue key, [MaybeNullWhen(false)] out CborValue value)
        {
            if (!_pairs.TryGetValue(key, out CborValue? result))
            {
                value = null!;
                return false;
            }

            value = result;
            return true;
        }

        public void Add(CborPair pair)
        {
            ((ICollection<CborPair>)_pairs)
                .Add(pair.Value != null ? pair : new CborPair(pair.Key, Null));
        }

        public void Clear()
        {
            _pairs.Clear();
        }

        public bool Contains(CborPair pair)
        {
            return ((ICollection<CborPair>)_pairs).Contains(pair);
        }

        public void CopyTo(CborPair[] array, int arrayIndex)
        {
            ((ICollection<CborPair>)_pairs).CopyTo(array, arrayIndex);
        }

        public bool Remove(CborPair pair)
        {
            return ((ICollection<CborPair>)_pairs).Remove(pair);
        }

        public IEnumerator<CborPair> GetEnumerator()
        {
            return _pairs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_pairs).GetEnumerator();
        }

        public static CborObject FromObject<T>(T obj, CborOptions? options = null)
        {
            options ??= CborOptions.Default;

            using ByteBufferWriter buffer = new ByteBufferWriter();
            ICborConverter<T> objectConverter = options.Registry.ConverterRegistry.Lookup<T>();
            CborWriter writer = new CborWriter(buffer);
            objectConverter.Write(ref writer, obj);

            ICborConverter<CborObject> cborObjectConverter = options.Registry.ConverterRegistry.Lookup<CborObject>();
            CborReader reader = new CborReader(buffer.WrittenSpan);
            return cborObjectConverter.Read(ref reader);
        }

        public T ToObject<T>(CborOptions? options = null)
        {
            options ??= CborOptions.Default;

            using ByteBufferWriter buffer = new ByteBufferWriter();
            ICborConverter<CborObject> cborObjectConverter = options.Registry.ConverterRegistry.Lookup<CborObject>();
            CborWriter writer = new CborWriter(buffer);
            cborObjectConverter.Write(ref writer, this);

            ICborConverter<T> objectConverter = options.Registry.ConverterRegistry.Lookup<T>();
            CborReader reader = new CborReader(buffer.WrittenSpan);
            return objectConverter.Read(ref reader);
        }

        public override bool TryGetMember(GetMemberBinder binder, [MaybeNullWhen(false)] out object result)
        {
            if (!TryGetValue(binder.Name, out CborValue? value))
            {
                return base.TryGetMember(binder, out result);
            }

            result = value;
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (value == null)
            {
                this[binder.Name] = CborValue.Null;
                return true;
            }

            switch (value)
            {
                case sbyte sByteValue:
                    this[binder.Name] = sByteValue;
                    return true;

                case byte byteValue:
                    this[binder.Name] = byteValue;
                    return true;

                case short shortValue:
                    this[binder.Name] = shortValue;
                    return true;

                case ushort ushortValue:
                    this[binder.Name] = ushortValue;
                    return true;

                case int intValue:
                    this[binder.Name] = intValue;
                    return true;

                case uint uintValue:
                    this[binder.Name] = uintValue;
                    return true;

                case long longValue:
                    this[binder.Name] = longValue;
                    return true;

                case ulong ulongValue:
                    this[binder.Name] = ulongValue;
                    return true;

                case decimal decimalValue:
                    this[binder.Name] = decimalValue;
                    return true;

                case float singleValue:
                    this[binder.Name] = singleValue;
                    return true;

                case double doubleValue:
                    this[binder.Name] = doubleValue;
                    return true;

                case bool boolValue:
                    this[binder.Name] = boolValue;
                    return true;

                case string stringValue:
                    this[binder.Name] = stringValue;
                    return true;

                case CborValue cborValue:
                    this[binder.Name] = cborValue;
                    return true;

                case int[] intArray:
                    this[binder.Name] = new CborArray(intArray.Select(i => (CborValue)i));
                    return true;
            }

            return base.TrySetMember(binder, value);
        }
    }
}
