using System;

namespace Dahomey.Cbor.Attributes
{
    /// <summary>
    /// Assigns an integer discriminator to a polymorphic type, instead of the string
    /// discriminator assigned by <see cref="CborDiscriminatorAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Integer discriminators are resolved by the <see cref="Serialization.Conventions.DefaultDiscriminatorConvention{T}"/>
    /// instantiated over <see cref="int"/>, which is registered by default. Like string discriminators, the value is
    /// written under the "_t" member name for map formats, and as the first item (behind a semantic tag) for
    /// <see cref="CborObjectFormat.Array"/>. To use a different member name, register the convention explicitly:
    /// <code>
    /// options.Registry.DiscriminatorConventionRegistry.RegisterConvention(
    ///     new DefaultDiscriminatorConvention&lt;int&gt;(options.Registry, "t"));
    /// </code>
    /// A type must not carry both <see cref="CborIntDiscriminatorAttribute"/> and
    /// <see cref="CborDiscriminatorAttribute"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class CborIntDiscriminatorAttribute : Attribute
    {
        public CborIntDiscriminatorAttribute(int discriminator)
        {
            Discriminator = discriminator;
        }

        public int Discriminator { get; set; }

        public CborDiscriminatorPolicy Policy { get; set; }
    }
}
