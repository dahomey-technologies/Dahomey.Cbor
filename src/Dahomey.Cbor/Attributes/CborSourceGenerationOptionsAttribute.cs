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

        /// <summary>How enum values are written. Mirrors <see cref="CborOptions.EnumFormat"/>.</summary>
        public ValueFormat EnumFormat { get; set; } = ValueFormat.WriteToInt;

        /// <summary>How <see cref="System.DateTime"/> is written. Mirrors <see cref="CborOptions.DateTimeFormat"/>.</summary>
        public DateTimeFormat DateTimeFormat { get; set; } = DateTimeFormat.ISO8601;

        /// <summary>
        /// Whether RFC 8746 typed arrays are read, written, both or neither. Mirrors
        /// <see cref="CborOptions.TypedArrayMode"/>.
        /// </summary>
        public TypedArrayMode TypedArrayMode { get; set; } = TypedArrayMode.Never;

        /// <summary>
        /// Which encoding a <see cref="decimal"/> is written as. Mirrors
        /// <see cref="CborOptions.DecimalFormat"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="DecimalFormat.DecimalFraction"/> is also the one setting that gives a
        /// <see cref="decimal"/> member a CDDL representation: the default form occupies a reserved
        /// major type 7 slot that no CDDL can describe, so a schema-generating context leaving this
        /// alone still reports CBOR1011 for such a member.
        /// </remarks>
        public DecimalFormat DecimalFormat { get; set; } = DecimalFormat.DecimalFloat;

        /// <summary>
        /// <see cref="CborOptions.TimeSpanFormat"/>.
        /// </summary>
        /// <remarks>
        /// The one setting that decides which mechanism handles a type rather than only which bytes it
        /// emits, so it has to be stated here: under the default a <see cref="TimeSpan"/> is collected
        /// as an object, and a value supplied at run time arrives after the context is generated.
        /// </remarks>
        public TimeSpanFormat TimeSpanFormat { get; set; } = TimeSpanFormat.Members;
    }
}
