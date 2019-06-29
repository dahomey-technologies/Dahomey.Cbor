using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborObject : CborValue, IComparable<CborObject>, IEquatable<CborObject>
    {
        public override CborValueType Type { get { return CborValueType.Object; } }
        public List<CborPair> Pairs { get; private set; }

        public CborObject()
        {
            Pairs = new List<CborPair>();
        }

        public CborObject(params CborPair[] pairs)
        {
            Pairs = new List<CborPair>(pairs);
        }

        public CborObject(Dictionary<string, CborValue> pairs)
        {
            Pairs = pairs.Select(kvp => new CborPair(kvp.Key, kvp.Value)).ToList();
        }

        public CborValue this[string name]
        {
            get { return Pairs.Where(p => p.Name == name).Select(p => p.Value).FirstOrDefault(); }
            set
            {
                CborPair pair = Pairs.Find(p => p.Name == name);
                if (pair == null)
                {
                    pair = new CborPair {Name = name};
                    Pairs.Add(pair);
                }

                pair.Value = value;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("{");

            bool separator = false;

            foreach (CborPair pair in Pairs)
            {
                if (separator)
                {
                    sb.Append(",");
                }

                separator = true;

                sb.Append(pair);
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

            using (var enumerator = Pairs.GetEnumerator())
            using (var otherEnumerator = other.Pairs.GetEnumerator())
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

        public bool Equals(CborObject other)
        {
            return other != null && Pairs.SequenceEqual(other.Pairs);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborObject value))
            {
                return false;
            }

            return Pairs.SequenceEqual(value.Pairs);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = 37 * hash + Type.GetHashCode();

            foreach(CborPair pair in Pairs)
            {
                hash = 37 * hash + pair.GetHashCode();
            }

            return hash;
        }
    }
}
