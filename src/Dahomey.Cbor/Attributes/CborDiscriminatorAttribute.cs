using System;

namespace Dahomey.Cbor.Attributes
{
    /// <summary>
    /// Specify the discriminators serialization policy
    /// </summary>
    public enum CborDiscriminatorPolicy
    {
        Default = 0,

        /// <summary>
        /// Discriminator will never be written
        /// </summary>
        Never,

        /// <summary>
        /// Discriminator will always be written
        /// </summary>
        Always,

        /// <summary>
        /// Discriminator will be written only if the declaring type and the actual type of the object being written differ.
        /// </summary>
        Auto
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class CborDiscriminatorAttribute : Attribute
    {
        public CborDiscriminatorAttribute(string discriminator)
        {
            Discriminator = discriminator;
        }

        public string Discriminator { get; set; }

        public CborDiscriminatorPolicy Policy { get; set; }
    }
}
