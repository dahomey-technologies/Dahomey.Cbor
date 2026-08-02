using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Walks outwards from the declared root types, classifying everything reachable and recording
    /// what must be registered before what.
    /// </summary>
    internal sealed class TypeCollector
    {
        private readonly Compilation _compilation;
        private readonly GenerationOptions _options;
        private readonly SourceProductionContext _spc;

        private readonly Dictionary<string, TypeModel> _models =
            new Dictionary<string, TypeModel>();

        public TypeCollector(Compilation compilation, GenerationOptions options, SourceProductionContext spc)
        {
            _compilation = compilation;
            _options = options;
            _spc = spc;
        }

        public void Collect(ITypeSymbol type)
        {
            string key = Key(type);

            if (_models.ContainsKey(key))
            {
                return;
            }

            TypeKind kind = Classify(type, out ITypeSymbol? element, out ITypeSymbol? value, out ITypeSymbol? underlying);

            TypeModel model = new TypeModel(type, kind)
            {
                ElementType = element,
                ValueType = value,
                UnderlyingType = underlying,
                IsTypedArray = kind == TypeKind.Array && IsTypedArrayElementType(element!),
            };

            // Insert before recursing so a cycle terminates.
            _models[key] = model;

            switch (kind)
            {
                case TypeKind.Primitive:
                    break;

                case TypeKind.Unsupported:
                    _spc.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedType,
                        type.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        DescribeWhyUnsupported(type)));
                    break;

                case TypeKind.Enum:
                    break;

                case TypeKind.ByteArray:
                    // ByteArrayConverter is concrete and takes no element converter, so there is
                    // nothing to collect and nothing to order.
                    break;

                case TypeKind.Nullable:
                    Collect(underlying!);
                    model.Dependencies.Add(underlying!);
                    break;

                case TypeKind.Array:
                case TypeKind.Collection:
                    Collect(element!);
                    model.Dependencies.Add(element!);
                    break;

                case TypeKind.Dictionary:
                    Collect(element!);
                    Collect(value!);
                    model.Dependencies.Add(element!);
                    model.Dependencies.Add(value!);
                    break;

                case TypeKind.Object:
                    CollectObject(type, model);
                    break;
            }
        }

        private void CollectObject(ITypeSymbol type, TypeModel model)
        {
            model.CanInstantiate = type is { IsAbstract: false, TypeKind: Microsoft.CodeAnalysis.TypeKind.Class or Microsoft.CodeAnalysis.TypeKind.Struct }
                && HasAccessibleParameterlessConstructor(type);

            ReadTypeLevelAttributes(type, model);

            foreach (ISymbol member in EnumerateMembers(type))
            {
                if (HasAttribute(member, "CborIgnoreAttribute"))
                {
                    continue;
                }

                ITypeSymbol memberType;
                bool canRead;
                bool canWrite;

                switch (member)
                {
                    case IPropertySymbol property:
                        if (property.IsIndexer || property.IsStatic || property.GetMethod is null)
                        {
                            continue;
                        }

                        memberType = property.Type;
                        canRead = true;
                        // An `init` accessor cannot be assigned from a lambda, so it is not usable as a
                        // setter here even though the property is technically settable.
                        canWrite = property.SetMethod is { IsInitOnly: false }
                            && property.SetMethod.DeclaredAccessibility == Accessibility.Public;
                        break;

                    case IFieldSymbol field:
                        if (field.IsStatic || field.IsImplicitlyDeclared)
                        {
                            continue;
                        }

                        memberType = field.Type;
                        canRead = true;
                        canWrite = !field.IsReadOnly;
                        break;

                    default:
                        continue;
                }

                (string cborName, int? cborIndex) = ResolveMemberName(member, model);

                if (model.ObjectFormat != "StringKeyMap" && cborIndex is null)
                {
                    _spc.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.MissingMemberIndex,
                        member.Locations.FirstOrDefault(),
                        type.Name,
                        member.Name,
                        model.ObjectFormat));
                    continue;
                }

                if (!canWrite)
                {
                    _spc.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.MemberNotDeserializable,
                        member.Locations.FirstOrDefault(),
                        type.Name,
                        member.Name));
                }

                model.Members.Add(new MemberModel(member.Name, cborName, cborIndex, memberType, canRead, canWrite));

                Collect(memberType);

                // A member's converter must exist before this object's converter is constructed —
                // except when it is this very type, which the self-reference break handles.
                if (Key(memberType) != Key(type))
                {
                    model.Dependencies.Add(memberType);
                }
            }
        }

        /// <summary>
        /// Members in the order the reflection path sees them: most-derived type first, then each base.
        /// Matching that order matters because it determines the order members appear on the wire, and
        /// the generated output is required to be byte-identical.
        /// </summary>
        private static IEnumerable<ISymbol> EnumerateMembers(ITypeSymbol type)
        {
            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is IPropertySymbol or IFieldSymbol
                        && member.DeclaredAccessibility == Accessibility.Public)
                    {
                        yield return member;
                    }
                }
            }
        }

        private void ReadTypeLevelAttributes(ITypeSymbol type, TypeModel model)
        {
            model.ObjectFormat = _options.ObjectFormat;
            model.DiscriminatorPolicy = _options.DiscriminatorPolicy;

            AttributeData? stringDiscriminator = null;
            AttributeData? intDiscriminator = null;

            foreach (AttributeData attribute in type.GetAttributes())
            {
                switch (attribute.AttributeClass?.Name)
                {
                    case "CborObjectFormatAttribute":
                        if (attribute.ConstructorArguments.Length == 1)
                        {
                            model.ObjectFormat = attribute.ConstructorArguments[0].Value switch
                            {
                                1 => "IntKeyMap",
                                2 => "Array",
                                _ => "StringKeyMap",
                            };
                        }
                        break;

                    case "CborDiscriminatorAttribute":
                        stringDiscriminator = attribute;
                        break;

                    case "CborIntDiscriminatorAttribute":
                        intDiscriminator = attribute;
                        break;
                }
            }

            if (stringDiscriminator is not null && intDiscriminator is not null)
            {
                _spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.ConflictingDiscriminators,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
                return;
            }

            AttributeData? discriminator = stringDiscriminator ?? intDiscriminator;

            if (discriminator is null)
            {
                return;
            }

            if (discriminator.ConstructorArguments.Length == 1)
            {
                TypedConstant argument = discriminator.ConstructorArguments[0];

                if (stringDiscriminator is not null)
                {
                    // Two renderings of the one value, because the two emitters need different
                    // escaping: FormatLiteral produces C# for the registration emitter to paste into
                    // generated code, and DiscriminatorText carries the raw string so the CDDL emitter
                    // can apply RFC 8610's own escapes instead.
                    string text = (string?)argument.Value ?? string.Empty;
                    model.DiscriminatorText = text;
                    model.Discriminator =
                        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true);
                }
                else
                {
                    // Invariant, not the compiler host's culture: a negative discriminator would
                    // otherwise pick up NumberFormatInfo.NegativeSign, which under some ICU locales is
                    // U+2212 rather than ASCII '-' -- neither compilable C# nor legal RFC 8610.
                    model.Discriminator =
                        ((int?)argument.Value ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            foreach (KeyValuePair<string, TypedConstant> named in discriminator.NamedArguments)
            {
                if (named.Key == "Policy")
                {
                    model.DiscriminatorPolicy = named.Value.Value switch
                    {
                        1 => "Never",
                        2 => "Always",
                        3 => "Auto",
                        _ => model.DiscriminatorPolicy,
                    };
                }
            }
        }

        private (string name, int? index) ResolveMemberName(ISymbol member, TypeModel model)
        {
            foreach (AttributeData attribute in member.GetAttributes())
            {
                if (attribute.AttributeClass?.Name != "CborPropertyAttribute")
                {
                    continue;
                }

                foreach (TypedConstant argument in attribute.ConstructorArguments)
                {
                    if (argument.Value is string explicitName)
                    {
                        return (explicitName, null);
                    }

                    if (argument.Value is int explicitIndex)
                    {
                        return (member.Name, explicitIndex);
                    }
                }
            }

            return (NamingConventions.Apply(_options.NamingConvention, member.Name), null);
        }

        private TypeKind Classify(
            ITypeSymbol type,
            out ITypeSymbol? element,
            out ITypeSymbol? value,
            out ITypeSymbol? underlying)
        {
            element = null;
            value = null;
            underlying = null;

            if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
            {
                return TypeKind.Enum;
            }

            if (IsPrimitive(type))
            {
                return TypeKind.Primitive;
            }

            if (type is IArrayTypeSymbol array)
            {
                if (array.Rank != 1)
                {
                    return TypeKind.Unsupported;
                }

                element = array.ElementType;

                // byte[] is a CBOR byte string, not an array of small integers — the reflection path
                // resolves it to ByteArrayConverter before the typed array provider is ever consulted.
                if (element.SpecialType == SpecialType.System_Byte)
                {
                    return TypeKind.ByteArray;
                }

                return TypeKind.Array;
            }

            if (type is INamedTypeSymbol named && named.IsGenericType)
            {
                string constructed = named.ConstructedFrom.ToDisplayString();

                if (constructed == "System.Nullable<T>")
                {
                    underlying = named.TypeArguments[0];
                    return TypeKind.Nullable;
                }

                // Dictionary<K,V> and anything implementing IDictionary<K,V>.
                INamedTypeSymbol? dictionaryInterface = named.AllInterfaces.FirstOrDefault(
                    i => i.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IDictionary<TKey, TValue>");

                if (dictionaryInterface is not null && named.TypeArguments.Length == 2)
                {
                    element = named.TypeArguments[0];
                    value = named.TypeArguments[1];
                    return HasAccessibleParameterlessConstructor(named)
                        ? TypeKind.Dictionary
                        : TypeKind.Unsupported;
                }

                // List<T> and anything implementing ICollection<T>.
                INamedTypeSymbol? collectionInterface = named.AllInterfaces.FirstOrDefault(
                    i => i.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.ICollection<T>");

                if (collectionInterface is not null && named.TypeArguments.Length == 1)
                {
                    element = named.TypeArguments[0];
                    return HasAccessibleParameterlessConstructor(named)
                        ? TypeKind.Collection
                        : TypeKind.Unsupported;
                }

                // Interfaces such as IList<T>/IEnumerable<T> need InterfaceCollectionConverter, which
                // is not emitted yet.
                if (named.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface)
                {
                    return TypeKind.Unsupported;
                }

                return TypeKind.Unsupported;
            }

            if (type.TypeKind is Microsoft.CodeAnalysis.TypeKind.Class
                or Microsoft.CodeAnalysis.TypeKind.Struct
                or Microsoft.CodeAnalysis.TypeKind.Interface)
            {
                return TypeKind.Object;
            }

            return TypeKind.Unsupported;
        }

        private static bool IsPrimitive(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Char:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                case SpecialType.System_String:
                case SpecialType.System_DateTime:
                case SpecialType.System_Object:
                    return true;
            }

            return type.ToDisplayString() is "System.Half" or "System.Guid" or "System.DateTimeOffset";
        }

        /// <summary>
        /// The ten RFC 8746 typed array element types.
        /// </summary>
        /// <remarks>
        /// MATCHED PAIR: this list duplicates the table in
        /// <c>src/Dahomey.Cbor/Serialization/Converters/TypedArrayTags.cs</c>. It cannot be shared —
        /// the generator is an analyzer assembly and must not reference the runtime library — so the
        /// two must be edited together; changing one alone silently desynchronises the generated path
        /// from the reflection path. The generated-vs-reflection byte-identity tests in
        /// <c>GeneratedTypedArrayTests</c> are what catch it.
        /// <para>
        /// <c>byte</c> is deliberately absent: <c>byte[]</c> is a plain CBOR byte string.
        /// </para>
        /// </remarks>
        private static bool IsTypedArrayElementType(ITypeSymbol element)
        {
            switch (element.SpecialType)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt64:
                case SpecialType.System_Int64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return true;
            }

            return element.ToDisplayString() == "System.Half";
        }

        private static bool HasAccessibleParameterlessConstructor(ITypeSymbol type)
        {
            if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct)
            {
                return true;
            }

            return type is INamedTypeSymbol named
                && named.InstanceConstructors.Any(c =>
                    c.Parameters.Length == 0 && c.DeclaredAccessibility != Accessibility.Private);
        }

        private static string DescribeWhyUnsupported(ITypeSymbol type)
        {
            if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface)
            {
                return "collection interfaces and abstract collection types are not generated yet; use a concrete type such as List<T>";
            }

            if (type is IArrayTypeSymbol { Rank: > 1 })
            {
                return "multi-dimensional arrays are not supported";
            }

            if (type is INamedTypeSymbol { IsGenericType: true })
            {
                return "this generic type is not a recognised collection or dictionary, and has no accessible parameterless constructor";
            }

            return "unrecognised shape";
        }

        private static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
        }

        private static string Key(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        /// <summary>
        /// Orders types so that every type's dependencies precede it, because converters resolve their
        /// members and elements during construction. Cycles are emitted in discovery order and rely on
        /// the runtime self-reference break in <c>DelegateMemberMapping</c>.
        /// </summary>
        public IReadOnlyList<TypeModel> InDependencyOrder()
        {
            List<TypeModel> ordered = new List<TypeModel>();
            HashSet<string> emitted = new HashSet<string>();
            HashSet<string> visiting = new HashSet<string>();

            void Visit(TypeModel model)
            {
                string key = Key(model.Symbol);

                if (emitted.Contains(key) || visiting.Contains(key))
                {
                    return;
                }

                visiting.Add(key);

                foreach (ITypeSymbol dependency in model.Dependencies)
                {
                    if (_models.TryGetValue(Key(dependency), out TypeModel? dependencyModel))
                    {
                        Visit(dependencyModel);
                    }
                }

                visiting.Remove(key);
                emitted.Add(key);
                ordered.Add(model);
            }

            foreach (TypeModel model in _models.Values.ToList())
            {
                Visit(model);
            }

            return ordered;
        }
    }
}
