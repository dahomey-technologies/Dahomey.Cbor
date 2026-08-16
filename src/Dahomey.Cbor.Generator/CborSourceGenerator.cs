using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Emits reflection-free CBOR registrations for classes deriving from
    /// <c>Dahomey.Cbor.Serialization.CborSerializerContext</c>.
    /// </summary>
    /// <remarks>
    /// Types are discovered transitively from the <c>[CborSerializable]</c> declarations: declaring
    /// <c>Person</c> is enough to also cover its <c>Address</c> member and its <c>List&lt;string&gt;</c>
    /// member. Only genuinely unsupported shapes are reported, so users are not made to enumerate every
    /// closed generic by hand.
    /// <para>
    /// The pipeline is built so that an edit which changes nothing about a context costs nothing. Both
    /// entry points are <see cref="SyntaxValueProvider.ForAttributeWithMetadataName"/>, answered from
    /// Roslyn's attribute index rather than by visiting every class in the compilation, and only values
    /// leave a step -- never an <see cref="ISymbol"/>, a <see cref="SyntaxNode"/> or a
    /// <see cref="Location"/>, each of which roots the whole compilation for as long as it is cached
    /// and compares by reference, so no later run could find its input unchanged.
    /// </para>
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class CborSourceGenerator : IIncrementalGenerator
    {
        private const string ContextBaseTypeName = "Dahomey.Cbor.Serialization.CborSerializerContext";
        private const string SerializableAttributeName = "Dahomey.Cbor.Attributes.CborSerializableAttribute";
        private const string DiscriminatorAttributeName = "Dahomey.Cbor.Attributes.CborDiscriminatorAttribute";
        private const string IntDiscriminatorAttributeName = "Dahomey.Cbor.Attributes.CborIntDiscriminatorAttribute";
        private const string CddlSchemaAttributeName = "Dahomey.Cbor.Attributes.CborCddlSchemaAttribute";

        /// <summary>Step names, so a test can assert what the pipeline reused.</summary>
        internal const string DiscriminatedTypesStep = "CborDiscriminatedTypes";
        internal const string ContextTargetsStep = "CborContextTargets";
        internal const string GeneratedContextsStep = "CborGeneratedContexts";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Every type carrying a discriminator, taken from the attribute index. CBOR1009 needs to
            // know which subtypes exist, and walking the assembly's type graph to find out costs the
            // whole graph on every keystroke -- for an answer that changes only when one of these two
            // attributes is added or removed.
            IncrementalValueProvider<EquatableArray<string>> discriminatedTypes =
                DiscriminatedTypeNames(context, DiscriminatorAttributeName)
                    .Combine(DiscriminatedTypeNames(context, IntDiscriminatorAttributeName))
                    .Select(static (pair, _) => new EquatableArray<string>(pair.Left.AddRange(pair.Right)))
                    .WithTrackingName(DiscriminatedTypesStep);

            IncrementalValuesProvider<ContextTarget> targets = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    SerializableAttributeName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, _) => ToContextTarget(ctx))
                .WithTrackingName(ContextTargetsStep);

            // The compilation is combined in rather than the target's own semantic model being trusted
            // for more than its own node: a context's output depends on the members of every type it
            // declares, and those live in other files. A step keyed on this file's syntax alone would
            // be reused unchanged after an edit to one of them, and emit stale registrations.
            IncrementalValuesProvider<GeneratedContext?> generated = targets
                .Combine(context.CompilationProvider)
                .Combine(discriminatedTypes)
                .Select(static (input, cancellationToken) => Generate(
                    input.Left.Left, input.Left.Right, input.Right, cancellationToken))
                .WithTrackingName(GeneratedContextsStep);

            context.RegisterSourceOutput(generated, static (spc, result) =>
            {
                if (result is null)
                {
                    return;
                }

                foreach (DiagnosticInfo diagnostic in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (result.HintName is not null && result.Source is not null)
                {
                    spc.AddSource(result.HintName, result.Source);
                }

                if (result.SchemaHintName is not null && result.SchemaSource is not null)
                {
                    spc.AddSource(result.SchemaHintName, result.SchemaSource);
                }
            });
        }

        private static IncrementalValueProvider<ImmutableArray<string>> DiscriminatedTypeNames(
            IncrementalGeneratorInitializationContext context, string attributeName)
        {
            return context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    attributeName,
                    predicate: static (node, _) => node is TypeDeclarationSyntax,
                    transform: static (ctx, _) => ctx.TargetSymbol is INamedTypeSymbol type
                        ? FullMetadataName(type)
                        : null)
                .Where(static name => name is not null)
                .Select(static (name, _) => name!)
                .Collect();
        }

        private static ContextTarget ToContextTarget(GeneratorAttributeSyntaxContext ctx)
        {
            INamedTypeSymbol symbol = (INamedTypeSymbol)ctx.TargetSymbol;
            ClassDeclarationSyntax declaration = (ClassDeclarationSyntax)ctx.TargetNode;

            return new ContextTarget(
                FullMetadataName(symbol),
                symbol.Name,
                declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)),
                LocationInfo.From(declaration.Identifier.GetLocation()));
        }

        /// <summary>
        /// The name <see cref="Compilation.GetTypeByMetadataName"/> takes: namespace-qualified, nested
        /// types joined with <c>+</c>, and an arity suffix on a generic.
        /// </summary>
        private static string FullMetadataName(INamedTypeSymbol symbol)
        {
            StringBuilder builder = new StringBuilder(symbol.MetadataName);

            for (INamedTypeSymbol? container = symbol.ContainingType;
                 container is not null;
                 container = container.ContainingType)
            {
                builder.Insert(0, '+').Insert(0, container.MetadataName);
            }

            if (!symbol.ContainingNamespace.IsGlobalNamespace)
            {
                builder.Insert(0, '.').Insert(0, symbol.ContainingNamespace.ToDisplayString());
            }

            return builder.ToString();
        }

        private static GeneratedContext? Generate(
            ContextTarget target,
            Compilation compilation,
            EquatableArray<string> discriminatedTypeNames,
            CancellationToken cancellationToken)
        {
            INamedTypeSymbol? contextBase = compilation.GetTypeByMetadataName(ContextBaseTypeName);
            INamedTypeSymbol? serializableAttribute = compilation.GetTypeByMetadataName(SerializableAttributeName);
            INamedTypeSymbol? contextSymbol = ResolveInCompilation(compilation, target.MetadataName);

            if (contextBase is null || serializableAttribute is null || contextSymbol is null)
            {
                // The Dahomey.Cbor reference is absent, or the declaration no longer resolves.
                return null;
            }

            if (!InheritsFrom(contextSymbol, contextBase))
            {
                // [CborSerializable] on something that is not a context. Not ours to report on.
                return null;
            }

            if (!target.IsPartial)
            {
                return new GeneratedContext(
                    null,
                    null,
                    new EquatableArray<DiagnosticInfo>(new[]
                    {
                        new DiagnosticInfo(
                            Diagnostics.ContextMustBePartial,
                            target.IdentifierLocation,
                            new EquatableArray<string>(new[] { target.Name })),
                    }));
            }

            List<DiagnosticInfo> diagnostics = new List<DiagnosticInfo>();
            GenerationOptions options = GenerationOptions.Read(contextSymbol, diagnostics);

            List<ITypeSymbol> roots = new List<ITypeSymbol>();

            foreach (AttributeData attribute in contextSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serializableAttribute))
                {
                    continue;
                }

                if (attribute.ConstructorArguments.Length == 1
                    && attribute.ConstructorArguments[0].Value is ITypeSymbol declaredType)
                {
                    roots.Add(declaredType);
                }
            }

            if (roots.Count == 0)
            {
                return diagnostics.Count == 0
                    ? null
                    : new GeneratedContext(null, null, new EquatableArray<DiagnosticInfo>(diagnostics));
            }

            TypeCollector collector = new TypeCollector(options, diagnostics);

            foreach (ITypeSymbol root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                collector.Collect(root);
            }

            collector.ReportUndeclaredSubtypes(
                DiscriminatedTypes(compilation, discriminatedTypeNames, cancellationToken));

            IReadOnlyList<TypeModel> ordered = collector.InDependencyOrder();

            string? schemaHintName = null;
            string? schemaSource = null;

            // Emitted before the diagnostics are frozen below, because the schema walk reports the two
            // failures only it can see: a type with no CDDL representation, and a polymorphic base whose
            // subtypes are not all reachable.
            if (contextSymbol.GetAttributes().Any(
                    a => a.AttributeClass?.ToDisplayString() == CddlSchemaAttributeName))
            {
                schemaHintName = CddlEmitter.HintName(contextSymbol);
                schemaSource = CddlEmitter.EmitSource(
                    contextSymbol, CddlEmitter.EmitSchema(ordered, roots, options, diagnostics));
            }

            return new GeneratedContext(
                Emitter.HintName(contextSymbol),
                Emitter.Emit(contextSymbol, options, ordered, roots),
                new EquatableArray<DiagnosticInfo>(diagnostics),
                schemaHintName,
                schemaSource);
        }

        /// <summary>
        /// Resolves the discriminated types found by the attribute index against the current
        /// compilation, so their base chains are read fresh rather than as they were when the file
        /// declaring them was last edited.
        /// </summary>
        private static IEnumerable<INamedTypeSymbol> DiscriminatedTypes(
            Compilation compilation,
            EquatableArray<string> metadataNames,
            CancellationToken cancellationToken)
        {
            foreach (string metadataName in metadataNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                INamedTypeSymbol? symbol = ResolveInCompilation(compilation, metadataName);

                if (symbol is not null)
                {
                    yield return symbol;
                }
            }
        }

        /// <summary>
        /// The compilation's own assembly first: <see cref="Compilation.GetTypeByMetadataName"/>
        /// returns null when a name is declared in more than one assembly, which a source type sharing
        /// its name with one in a reference would hit.
        /// </summary>
        private static INamedTypeSymbol? ResolveInCompilation(Compilation compilation, string metadataName)
        {
            return compilation.Assembly.GetTypeByMetadataName(metadataName)
                ?? compilation.GetTypeByMetadataName(metadataName);
        }

        private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A candidate context, carrying only what survives being cached.</summary>
    internal sealed record ContextTarget(
        string MetadataName,
        string Name,
        bool IsPartial,
        LocationInfo? IdentifierLocation);

    /// <summary>
    /// What a context produced: the sources to add, and what to report. All are values, so an edit
    /// that leaves them unchanged stops here instead of adding a syntax tree and re-triggering
    /// everything that depends on one.
    /// </summary>
    /// <remarks>
    /// The CDDL schema is a second, independently-gated source rather than part of the first: it is
    /// emitted only for a context carrying <c>[CborCddlSchema]</c>, while the registrations are emitted
    /// for every context. Both stay nullable strings so the record compares by value, which is what
    /// lets an unchanged context stop at this step.
    /// </remarks>
    internal sealed record GeneratedContext(
        string? HintName,
        string? Source,
        EquatableArray<DiagnosticInfo> Diagnostics,
        string? SchemaHintName = null,
        string? SchemaSource = null);

    /// <summary>Context-wide settings read from <c>[CborSourceGenerationOptions]</c>.</summary>
    internal sealed class GenerationOptions
    {
        public string? NamingConvention { get; private set; }
        public string ObjectFormat { get; private set; } = "StringKeyMap";
        public string? DiscriminatorPolicy { get; private set; }
        public ulong? DiscriminatorSemanticTag { get; private set; }
        public int? MaxDepth { get; private set; }
        public string? EnumFormat { get; private set; }
        public string? DateTimeFormat { get; private set; }
        public string? TypedArrayMode { get; private set; }
        public string? DecimalFormat { get; private set; }

        /// <summary>
        /// Whether this context writes RFC 8746 typed arrays, which is the only half of
        /// <c>TypedArrayMode</c> a schema can describe: CDDL says what a written document looks like,
        /// and a context that only reads typed arrays still writes ordinary CBOR arrays.
        /// </summary>
        public bool WritesTypedArrays =>
            TypedArrayMode is "WriteLittleEndian" or "ReadWriteLittleEndian";

        /// <summary>
        /// Whether this context writes RFC 8949 §3.4.4 decimal fractions, which is the only form of
        /// <c>decimal</c> a schema can describe - the default encoding is a reserved major type 7 slot
        /// with no CDDL spelling. Like <see cref="WritesTypedArrays"/> this is a write-side question:
        /// reading tag 4 is unconditional and says nothing about the documents this context produces.
        /// </summary>
        public bool WritesDecimalFractions => DecimalFormat is "DecimalFraction";

        /// <summary>
        /// Whether an <c>Array</c> rule labels each entry with its member name, from
        /// <c>[CborCddlSchema(MemberNames = true)]</c>. It rides here rather than being threaded
        /// separately because every method that emits a shape already takes these options.
        /// </summary>
        public bool CddlMemberNames { get; private set; }

        public static GenerationOptions Read(INamedTypeSymbol contextSymbol, List<DiagnosticInfo> diagnostics)
        {
            GenerationOptions options = new GenerationOptions();

            // Read before the early return below: this setting lives on [CborCddlSchema], so a context
            // carrying that attribute and no [CborSourceGenerationOptions] must still see it.
            AttributeData? schemaAttribute = contextSymbol.GetAttributes().FirstOrDefault(
                a => a.AttributeClass?.ToDisplayString() == "Dahomey.Cbor.Attributes.CborCddlSchemaAttribute");

            if (schemaAttribute is not null)
            {
                foreach (KeyValuePair<string, TypedConstant> named in schemaAttribute.NamedArguments)
                {
                    if (named.Key == "MemberNames" && named.Value.Value is bool memberNames)
                    {
                        options.CddlMemberNames = memberNames;
                    }
                }
            }

            AttributeData? attribute = contextSymbol.GetAttributes().FirstOrDefault(
                a => a.AttributeClass?.ToDisplayString() == "Dahomey.Cbor.Attributes.CborSourceGenerationOptionsAttribute");

            if (attribute is null)
            {
                return options;
            }

            foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
            {
                switch (named.Key)
                {
                    case "NamingConvention":
                        if (named.Value.Value is ITypeSymbol convention)
                        {
                            string name = convention.Name;

                            if (!NamingConventions.IsSupported(name))
                            {
                                diagnostics.Add(DiagnosticInfo.Create(
                                    Diagnostics.UnsupportedNamingConvention,
                                    contextSymbol.Locations.FirstOrDefault(),
                                    convention.ToDisplayString()));
                            }
                            else
                            {
                                options.NamingConvention = name;
                            }
                        }
                        break;

                    case "ObjectFormat":
                        options.ObjectFormat = FormatName(named.Value.Value);
                        break;

                    case "DiscriminatorPolicy":
                        options.DiscriminatorPolicy = PolicyName(named.Value.Value);
                        break;

                    case "DiscriminatorSemanticTag":
                        if (named.Value.Value is ulong tag)
                        {
                            options.DiscriminatorSemanticTag = tag;
                        }
                        break;

                    case "MaxDepth":
                        if (named.Value.Value is int maxDepth)
                        {
                            options.MaxDepth = maxDepth;
                        }
                        break;

                    case "EnumFormat":
                        options.EnumFormat = named.Value.Value switch
                        {
                            1 => "WriteToString",
                            _ => null,
                        };
                        break;

                    case "DateTimeFormat":
                        options.DateTimeFormat = named.Value.Value switch
                        {
                            1 => "Unix",
                            2 => "UnixMilliseconds",
                            _ => null,
                        };
                        break;

                    // TypedArrayMode is a [Flags] enum -- Never = 0, Read = 1, WriteLittleEndian = 2,
                    // ReadWriteLittleEndian = Read | WriteLittleEndian -- so every representable value
                    // is named here and emitted as the member the user wrote rather than as a bitwise
                    // expression. Never needs no assignment: it is the library default.
                    case "TypedArrayMode":
                        options.TypedArrayMode = named.Value.Value switch
                        {
                            1 => "Read",
                            2 => "WriteLittleEndian",
                            3 => "ReadWriteLittleEndian",
                            _ => null,
                        };
                        break;

                    case "DecimalFormat":
                        options.DecimalFormat = named.Value.Value switch
                        {
                            1 => "DecimalFraction",
                            _ => null,
                        };
                        break;
                }
            }

            return options;
        }

        private static string FormatName(object? value)
        {
            return value switch
            {
                1 => "IntKeyMap",
                2 => "Array",
                _ => "StringKeyMap",
            };
        }

        private static string? PolicyName(object? value)
        {
            return value switch
            {
                1 => "Never",
                2 => "Always",
                3 => "Auto",
                _ => null,
            };
        }
    }
}
