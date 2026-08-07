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
        /// <summary>
        /// A type carrying a feature the generator does not reproduce. Reporting is the whole
        /// contract: the reflection path honours these attributes, so generating without them would
        /// change the bytes silently rather than fail.
        /// </summary>
        public static readonly DiagnosticDescriptor UnsupportedFeature = new(
            id: "CBOR1007",
            title: "CBOR feature is not supported by source generation",
            messageFormat: "'{0}' uses {1}, which CBOR source generation does not reproduce. The reflection path honours it, so a generated context would silently produce different bytes",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// The reflection path serializes some non-public members; the generated path cannot reach
        /// them from outside the declaring type. Dropping one is a wire-format change.
        /// </summary>
        public static readonly DiagnosticDescriptor NonPublicMember = new(
            id: "CBOR1008",
            title: "Non-public member cannot be source-generated",
            messageFormat: "'{0}.{1}' is {2} and is serialized by the reflection path, but source generation cannot access it. Make it public, or exclude it with [CborIgnore]",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// Base to derived is not a member edge, so a subtype is never reached by walking outwards
        /// from a root. Without its own declaration it is not registered, and a polymorphic read of it
        /// fails or silently resolves to the fallback type.
        /// </summary>
        public static readonly DiagnosticDescriptor SubtypeNotDeclared = new(
            id: "CBOR1009",
            title: "Discriminated subtype is not declared on any context",
            messageFormat: "'{0}' carries a discriminator and derives from '{1}', which is declared, but is not itself declared with [CborSerializable]. Polymorphic reads will not resolve it",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// Registered fine and fails at run time otherwise: the reflection path can reach a
        /// non-public constructor or a creator mapping, and the generated factory is a plain
        /// <c>new T()</c>.
        /// </summary>
        public static readonly DiagnosticDescriptor NoParameterlessConstructor = new(
            id: "CBOR1010",
            title: "Type has no accessible parameterless constructor",
            messageFormat: "'{0}' has no accessible parameterless constructor, so a generated context cannot create one when reading. Add one, or exclude the type",
            Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MemberNotDeserializable = new(
            id: "CBOR1006",
            title: "Member cannot be deserialized",
            messageFormat: "'{0}.{1}' has no usable setter, so it will be written but never read back. Add a settable setter, or a creator mapping",
            Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
