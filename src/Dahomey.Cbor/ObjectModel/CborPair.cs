using System;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborPair : IComparable<CborPair>, IEquatable<CborPair>
    {
        public string Name { get; set; }
        public CborValue Value { get; set; }

        public CborPair()
        {
        }

        public CborPair(string name, CborValue value)
        {
            Name = name;
            Value = value ?? CborValue.Null;
        }

        public override string ToString()
        {
            return string.Format("\"{0}\":{1}", Name, Value);
        }

        public int CompareTo(CborPair other)
        {
            if (other == null)
            {
                return 1;
            }

            int cmp = Name.CompareTo(other.Name);
            if (cmp != 0)
            {
                return cmp;
            }
            return Value.CompareTo(other.Value);
        }

        public bool Equals(CborPair other)
        {
            return other != null && Name == other.Name && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is CborPair value))
            {
                return false;
            }

            return Equals(value);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = 37 * hash + Name.GetHashCode();
            hash = 37 * hash + Value.GetHashCode();
            return hash;
        }
    }
}
