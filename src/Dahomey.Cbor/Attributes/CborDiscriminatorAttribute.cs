using System;

namespace Dahomey.Cbor.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CborDiscriminatorAttribute : Attribute
    {
        public CborDiscriminatorAttribute(string discriminator)
        {
            Discriminator = discriminator;
        }

        public string Discriminator { get; set; }
    }
}
