using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CborIgnoreIfDefaultAttribute : Attribute
    {
    }
}
