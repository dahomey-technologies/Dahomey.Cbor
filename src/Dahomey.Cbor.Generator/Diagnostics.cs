using Microsoft.CodeAnalysis;

namespace Dahomey.Cbor.Generator
{
    internal static class Diagnostics
    {
        private const string Category = "Dahomey.Cbor.Generator";

        /// <summary>
        /// The context class must be <c>partial</c>, since the generator supplies the other half.
        /// </summary>
        public static readonly DiagnosticDescriptor ContextMustBePartial = new(
            id: "CBOR1001",
            title: "CBOR serializer context must be partial",
            messageFormat: "Context '{0}' must be declared 'partial' so the generated registrations can be added to it",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// The whole point of generating: an unsupported member type would resolve through the
        /// reflection provider chain, which works on CoreCLR and fails under Native AOT. Reporting it
        /// at build time is what turns a run-time <c>MissingMethodException</c> into a compile error.
        /// </summary>
        public static readonly DiagnosticDescriptor UnsupportedType = new(
            id: "CBOR1002",
            title: "Type is not supported by CBOR source generation",
            messageFormat: "'{0}' cannot be handled by CBOR source generation ({1}). It would fall back to runtime reflection, which fails under Native AOT",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedNamingConvention = new(
            id: "CBOR1003",
            title: "Naming convention is not supported by CBOR source generation",
            messageFormat: "Naming convention '{0}' cannot be reproduced at compile time. Use one of the built-in conventions, or set member names explicitly with [CborProperty]",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// A type with both discriminator attributes is rejected by the reflection path at run time;
        /// the generator can say so at build time instead.
        /// </summary>
        public static readonly DiagnosticDescriptor ConflictingDiscriminators = new(
            id: "CBOR1004",
            title: "Type has conflicting discriminator attributes",
            messageFormat: "'{0}' is annotated with both [CborDiscriminator] and [CborIntDiscriminator]; only one is allowed",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// Non-StringKeyMap formats key members by index, so every member needs
        /// <c>[CborProperty(index)]</c>. The reflection path throws at converter construction.
        /// </summary>
        public static readonly DiagnosticDescriptor MissingMemberIndex = new(
            id: "CBOR1005",
            title: "Member needs an explicit index",
            messageFormat: "'{0}.{1}' needs [CborProperty(index)] because '{0}' uses the {2} object format",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// Not an error: the type still serializes, it just cannot be populated on read. Worth a
        /// warning because it is usually an oversight (a get-only or <c>init</c>-only property).
        /// </summary>
        public static readonly DiagnosticDescriptor MemberNotDeserializable = new(
            id: "CBOR1006",
            title: "Member cannot be deserialized",
            messageFormat: "'{0}.{1}' has no usable setter, so it will be written but never read back. Add a settable setter, or a creator mapping",
            Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// A schema that quietly omits a member is worse than no schema, so a type with no CDDL
        /// representation is an error rather than a gap in the output. <c>{1}</c> is the place that
        /// would have been omitted, already quoted and phrased by the caller, because the two callers
        /// describe different places: a member of a type, and a declared root type with no member above
        /// it to name.
        /// </summary>
        public static readonly DiagnosticDescriptor NoCddlRepresentation = new(
            id: "CBOR1007",
            title: "Type has no CDDL representation",
            messageFormat: "'{0}' has no CDDL representation, so {1} cannot appear in the emitted schema",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// A type choice missing a subtype describes a narrower contract than the serializer actually
        /// writes, which is the quiet failure the schema exists to prevent. Only reachable for a type
        /// that cannot be instantiated at all -- an abstract class or an interface -- since a concrete
        /// class with no subtypes is simply described by its own rule.
        /// </summary>
        public static readonly DiagnosticDescriptor IncompletePolymorphicSchema = new(
            id: "CBOR1008",
            title: "Polymorphic schema is incomplete",
            messageFormat: "'{0}' is polymorphic but no subtype carrying a discriminator is reachable from this context; declare each subtype with [CborSerializable] and give it a [CborDiscriminator] or [CborIntDiscriminator] so the type choice can tell them apart",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
