using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CborNamingConventionAttribute : Attribute
    {
        public CborNamingConventionAttribute(Type namingConventionType)
        {
            NamingConventionType = namingConventionType;
        }

        public Type NamingConventionType { get; set; }
    }
}
