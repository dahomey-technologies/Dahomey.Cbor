using System;

namespace Dahomey.Cbor.Attributes
{
    /// <summary>
    /// Context-wide defaults for CBOR source generation, mirroring the equivalent
    /// <see cref="CborOptions"/> settings so a generated context needs no imperative setup.
    /// </summary>
    /// <remarks>
    /// Per-type attributes win over these: <see cref="CborObjectFormatAttribute"/> on a type overrides
    /// <see cref="ObjectFormat"/>, and so on.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CborSourceGenerationOptionsAttribute : Attribute
    {
        /// <summary>
        /// Naming convention applied to member names. Must implement
        /// <see cref="Serialization.Conventions.INamingConvention"/> and have a public parameterless
        /// constructor. Null leaves member names as declared.
        /// </summary>
        public Type? NamingConvention { get; set; }

        /// <summary>Default object format for generated types.</summary>
        public CborObjectFormat ObjectFormat { get; set; } = CborObjectFormat.StringKeyMap;

        /// <summary>Default discriminator policy for generated types.</summary>
        public CborDiscriminatorPolicy DiscriminatorPolicy { get; set; } = CborDiscriminatorPolicy.Default;

        /// <summary>
        /// Semantic tag preceding the discriminator when <see cref="ObjectFormat"/> is
        /// <see cref="CborObjectFormat.Array"/>.
        /// </summary>
        public ulong DiscriminatorSemanticTag { get; set; } = 39;

        /// <summary>Maximum nesting depth for reading and writing.</summary>
        public int MaxDepth { get; set; } = 64;
    }
}
