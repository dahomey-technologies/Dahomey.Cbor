using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Constructor)]
    public class CborConstructorAttribute : Attribute
    {
        public string[] MemberNames { get; set; } 

        public CborConstructorAttribute()
        {
        }

        public CborConstructorAttribute(params string[] memberNames)
        {
            MemberNames = memberNames;
        }
    }
}
