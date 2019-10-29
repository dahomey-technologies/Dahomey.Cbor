using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class CborLengthModeAttribute : Attribute
    {
        public LengthMode LengthMode { get; set; }
    }
}
