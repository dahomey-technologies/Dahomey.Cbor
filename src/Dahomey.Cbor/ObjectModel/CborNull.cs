using System;

namespace Dahomey.Cbor.ObjectModel
{
    public class CborNull : CborValue, IComparable<CborNull>, IEquatable<CborNull>
    {
        public override CborValueType Type { get { return CborValueType.Null; } }

        internal CborNull()
        {
        }

        public override string ToString()
        {
            return "null";
        }

        public override int CompareTo(CborValue? other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other is CborNull otherBoolean)
            {
                return CompareTo(otherBoolean);
            }

            return CompareTypeTo(other);
        }

        public int CompareTo(CborNull? other)
        {
            return other == null ? 1 : 0;
        }

        public bool Equals(CborNull? other)
        {
            return other != null;
        }

        public override bool Equals(object? obj)
        {
            return obj != null && (obj is CborNull);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }
    }
}
