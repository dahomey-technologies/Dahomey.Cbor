using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Constructor)]
    public class CborConstructorAttribute : Attribute
    {
        public string[]? MemberNames { get; private set; }
        public int[]? MemberIndexes { get; private set; }

        public CborConstructorAttribute()
        {
        }

        public CborConstructorAttribute(params string[] memberNames)
        {
            MemberNames = memberNames;
        }

        public CborConstructorAttribute(params int[] memberIndexes)
        {
            MemberIndexes = memberIndexes;
        }
    }
}
