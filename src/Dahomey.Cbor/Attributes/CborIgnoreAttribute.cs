using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CborIgnoreAttribute : Attribute
    {
    }
}
