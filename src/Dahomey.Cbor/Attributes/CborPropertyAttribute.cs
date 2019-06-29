using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class CborPropertyAttribute : Attribute
    {
        public string PropertyName { get; set; }

        public CborPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}
