using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public sealed class CborSourceGenerator : IIncrementalGenerator
    {
        private const string ContextBaseTypeName = "Dahomey.Cbor.Serialization.CborSerializerContext";
        private const string SerializableAttributeName = "Dahomey.Cbor.Attributes.CborSerializableAttribute";
        private const string OptionsAttributeName = "Dahomey.Cbor.Attributes.CborSourceGenerationOptionsAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            IncrementalValuesProvider<ClassDeclarationSyntax> candidates = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                    transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node);

            IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> input =
                context.CompilationProvider.Combine(candidates.Collect());

            context.RegisterSourceOutput(input, static (spc, source) =>
            {
                (Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes) = source;

                INamedTypeSymbol? contextBase = compilation.GetTypeByMetadataName(ContextBaseTypeName);
                INamedTypeSymbol? serializableAttribute = compilation.GetTypeByMetadataName(SerializableAttributeName);

                if (contextBase is null || serializableAttribute is null)
                {
                    // The Dahomey.Cbor reference is absent; nothing to do.
                    return;
                }

                foreach (ClassDeclarationSyntax declaration in classes.Distinct())
                {
                    SemanticModel model = compilation.GetSemanticModel(declaration.SyntaxTree);

                    if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol contextSymbol)
                    {
                        continue;
                    }

                    if (!InheritsFrom(contextSymbol, contextBase))
                    {
                        continue;
                    }

                    if (!declaration.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.ContextMustBePartial,
                            declaration.Identifier.GetLocation(),
                            contextSymbol.Name));
                        continue;
                    }

                    Emit(spc, compilation, contextSymbol, serializableAttribute);
                }
            });
        }

        private static void Emit(
            SourceProductionContext spc,
            Compilation compilation,
            INamedTypeSymbol contextSymbol,
            INamedTypeSymbol serializableAttribute)
        {
            GenerationOptions options = GenerationOptions.Read(contextSymbol, spc);

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
                return;
            }

            TypeCollector collector = new TypeCollector(compilation, options, spc);
            foreach (ITypeSymbol root in roots)
            {
                collector.Collect(root);
            }

            IReadOnlyList<TypeModel> ordered = collector.InDependencyOrder();

            string source = Emitter.Emit(contextSymbol, options, ordered, roots);
            spc.AddSource($"{contextSymbol.Name}.CborContext.g.cs", source);
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

    /// <summary>Context-wide settings read from <c>[CborSourceGenerationOptions]</c>.</summary>
    internal sealed class GenerationOptions
    {
        public string? NamingConvention { get; private set; }
        public string ObjectFormat { get; private set; } = "StringKeyMap";
        public string? DiscriminatorPolicy { get; private set; }
        public ulong? DiscriminatorSemanticTag { get; private set; }
        public int? MaxDepth { get; private set; }

        public static GenerationOptions Read(INamedTypeSymbol contextSymbol, SourceProductionContext spc)
        {
            GenerationOptions options = new GenerationOptions();

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
                                spc.ReportDiagnostic(Diagnostic.Create(
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
