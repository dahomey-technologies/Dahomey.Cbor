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
        private readonly GenerationOptions _options;
        private readonly List<DiagnosticInfo> _diagnostics;

        private readonly Dictionary<string, TypeModel> _models =
            new Dictionary<string, TypeModel>();

        public TypeCollector(GenerationOptions options, List<DiagnosticInfo> diagnostics)
        {
            _options = options;
            _diagnostics = diagnostics;
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
                    _diagnostics.Add(DiagnosticInfo.Create(
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

            // An abstract base is never instantiated -- its subtypes are -- so only a concrete type
            // without a constructor is a problem. The reflection path can reach a non-public
            // constructor or a [CborConstructor] creator mapping; a generated factory is `new T()`.
            if (!model.CanInstantiate
                && type is { IsAbstract: false, TypeKind: Microsoft.CodeAnalysis.TypeKind.Class })
            {
                _diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.NoParameterlessConstructor,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
            }

            ReadTypeLevelAttributes(type, model);
            ReportUnsupportedFeatures(type);

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
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.MissingMemberIndex,
                        member.Locations.FirstOrDefault(),
                        type.Name,
                        member.Name,
                        model.ObjectFormat));
                    continue;
                }

                if (!canWrite)
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
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
        /// Features the reflection path honours and the generator does not reproduce. Each is
        /// reported rather than silently dropped, because dropping one changes the bytes or the
        /// behaviour without any signal at all -- and <c>[CborConverter]</c> in particular means
        /// discarding a converter the user wrote themselves.
        /// </summary>
        private static readonly (string Attribute, string Description)[] UnsupportedTypeFeatures =
        {
            ("CborConverterAttribute", "[CborConverter]"),
            ("CborConstructorAttribute", "[CborConstructor]"),
            ("CborNamingConventionAttribute", "[CborNamingConvention]"),
            ("CborLengthModeAttribute", "[CborLengthMode]"),
        };

        private static readonly (string Attribute, string Description)[] UnsupportedMemberFeatures =
        {
            ("CborConverterAttribute", "[CborConverter]"),
            ("CborRequiredAttribute", "[CborRequired]"),
            ("CborIgnoreIfDefaultAttribute", "[CborIgnoreIfDefault]"),
            ("CborLengthModeAttribute", "[CborLengthMode]"),
            ("DefaultValueAttribute", "[DefaultValue]"),
        };

        private void ReportUnsupportedFeatures(ITypeSymbol type)
        {
            foreach ((string attribute, string description) in UnsupportedTypeFeatures)
            {
                if (HasAttribute(type, attribute))
                {
                    Report(type.Locations.FirstOrDefault(), type.ToDisplayString(), description);
                }
            }

            // Callbacks and ShouldSerialize are conventions rather than attributes on the type, so
            // they are detected the same way DefaultObjectMappingConvention detects them.
            foreach (ISymbol member in AllMembers(type))
            {
                if (member is IMethodSymbol method)
                {
                    if (HasAttribute(method, "OnDeserializingAttribute"))
                    {
                        Report(method.Locations.FirstOrDefault(), type.ToDisplayString(), "[OnDeserializing]");
                    }

                    if (HasAttribute(method, "OnDeserializedAttribute"))
                    {
                        Report(method.Locations.FirstOrDefault(), type.ToDisplayString(), "[OnDeserialized]");
                    }

                    if (method.Name.StartsWith("ShouldSerialize", System.StringComparison.Ordinal)
                        && method.Name.Length > "ShouldSerialize".Length
                        && method.Parameters.Length == 0
                        && method.ReturnType.SpecialType == SpecialType.System_Boolean)
                    {
                        Report(method.Locations.FirstOrDefault(), type.ToDisplayString(), $"a {method.Name}() method");
                    }

                    continue;
                }

                if (member is not (IPropertySymbol or IFieldSymbol))
                {
                    continue;
                }

                foreach ((string attribute, string description) in UnsupportedMemberFeatures)
                {
                    if (HasAttribute(member, attribute))
                    {
                        Report(member.Locations.FirstOrDefault(), $"{type.Name}.{member.Name}", description);
                    }
                }
            }

            if (type.AllInterfaces.Any(i => i.ToDisplayString() == "System.ComponentModel.ISupportInitialize"))
            {
                Report(type.Locations.FirstOrDefault(), type.ToDisplayString(), "ISupportInitialize");
            }

            void Report(Location? location, string owner, string feature)
            {
                _diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.UnsupportedFeature, location, owner, feature));
            }
        }

        private static IEnumerable<ISymbol> AllMembers(ITypeSymbol type)
        {
            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    yield return member;
                }
            }
        }

        /// <summary>
        /// Members in the order the reflection path sees them: most-derived type first, then each base.
        /// Matching that order matters because it determines the order members appear on the wire, and
        /// the generated output is required to be byte-identical.
        /// </summary>
        private IEnumerable<ISymbol> EnumerateMembers(ITypeSymbol type)
        {
            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is not (IPropertySymbol or IFieldSymbol))
                    {
                        continue;
                    }

                    if (member.DeclaredAccessibility == Accessibility.Public)
                    {
                        yield return member;
                        continue;
                    }

                    if (IsSerializedByTheReflectionPath(member))
                    {
                        _diagnostics.Add(DiagnosticInfo.Create(
                            Diagnostics.NonPublicMember,
                            member.Locations.FirstOrDefault(),
                            type.Name,
                            member.Name,
                            member.DeclaredAccessibility.ToString().ToLowerInvariant()));
                    }
                }
            }
        }

        /// <summary>
        /// Whether <c>DefaultObjectMappingConvention</c> would serialize this non-public member, and
        /// therefore whether dropping it changes the bytes.
        /// </summary>
        /// <remarks>
        /// That convention excludes a property whose getter is protected, private or static, and a
        /// field that is private or static — unless the member carries <c>[CborProperty]</c>, which
        /// overrides the filter entirely. So an internal property, an internal or protected field, and
        /// anything at all with the attribute, are all serialized today.
        /// </remarks>
        private static bool IsSerializedByTheReflectionPath(ISymbol member)
        {
            if (member.IsStatic)
            {
                return false;
            }

            if (HasAttribute(member, "CborIgnoreAttribute"))
            {
                return false;
            }

            if (HasAttribute(member, "CborPropertyAttribute"))
            {
                return true;
            }

            return member switch
            {
                IPropertySymbol property => property.GetMethod is
                    { DeclaredAccessibility: Accessibility.Internal or Accessibility.ProtectedOrInternal },
                IFieldSymbol field => field.DeclaredAccessibility
                    is Accessibility.Internal or Accessibility.Protected or Accessibility.ProtectedOrInternal,
                _ => false,
            };
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
                _diagnostics.Add(DiagnosticInfo.Create(
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
                model.Discriminator = stringDiscriminator is not null
                    ? Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral((string?)argument.Value ?? string.Empty, quote: true)
                    : ((int?)argument.Value ?? 0).ToString();
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

                // byte[] is a CBOR byte string, not an array of small integers -- the reflection
                // path resolves it to ByteArrayConverter, and a generated ArrayConverter<byte>
                // would write 83 01 02 03 where the reflection path writes 43 01 02 03.
                if (element.SpecialType == SpecialType.System_Byte)
                {
                    return TypeKind.ByteArray;
                }

                return TypeKind.Array;
            }

            if (HasNoConcreteConverter(type))
            {
                return TypeKind.Unsupported;
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

        /// <summary>
        /// Types the reflection path resolves through <c>ObjectConverterProvider</c> rather than a
        /// concrete converter, so a generated context has nothing to register for them.
        /// </summary>
        private static bool HasNoConcreteConverter(ITypeSymbol type)
        {
            return type.SpecialType == SpecialType.System_Object
                || type.ToDisplayString() is "System.Half" or "System.Guid" or "System.DateTimeOffset";
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
                    // System_Object is deliberately absent -- see HasNoConcreteConverter.
                    return true;
            }

            // System.Half, System.Guid, System.DateTimeOffset and System.Object are deliberately absent.
            // PrimitiveConverterProvider has no case for any of them, so at run time they fall through
            // to ObjectConverterProvider and reach MakeGenericType -- the exact failure a generated
            // context exists to prevent, and one the AOT analyzer cannot see either. Classifying them
            // as unsupported turns that into a build error. See UnsupportedReason.
            return false;
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
            if (HasNoConcreteConverter(type))
            {
                return type.SpecialType == SpecialType.System_Object
                    ? "object has no concrete converter, so its value would be resolved by reflection at run time; declare the actual type instead"
                    : $"{type.ToDisplayString()} has no concrete converter in PrimitiveConverterProvider, so the reflection path builds one through MakeGenericType; register a custom converter for it with [CborConverter]";
            }

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
        /// Reports discriminated subtypes of a collected type that no context declares.
        /// </summary>
        /// <remarks>
        /// Base to derived is not a member edge, so walking outwards from the roots never reaches a
        /// subtype. An undeclared one is never registered, <c>TryRegisterType</c> is never called for
        /// it, and a polymorphic read either fails or resolves to the fallback type -- with nothing
        /// said at build time. "Only roots need declaring" holds for member graphs and not for
        /// hierarchies, which is the case where forgetting one is expensive.
        /// <para>
        /// The candidates come from Roslyn's attribute index rather than from a walk of the assembly's
        /// type graph, so the cost is proportional to the number of discriminated types rather than to
        /// the size of the compilation. Both see source only: a subtype in a referenced assembly
        /// cannot be declared on a context in this one anyway.
        /// </para>
        /// </remarks>
        public void ReportUndeclaredSubtypes(IEnumerable<INamedTypeSymbol> discriminatedTypes)
        {
            foreach (INamedTypeSymbol candidate in discriminatedTypes)
            {
                if (_models.ContainsKey(Key(candidate)))
                {
                    continue;
                }

                for (INamedTypeSymbol? baseType = candidate.BaseType;
                     baseType is not null && baseType.SpecialType != SpecialType.System_Object;
                     baseType = baseType.BaseType)
                {
                    if (_models.ContainsKey(Key(baseType)))
                    {
                        _diagnostics.Add(DiagnosticInfo.Create(
                            Diagnostics.SubtypeNotDeclared,
                            candidate.Locations.FirstOrDefault(),
                            candidate.ToDisplayString(),
                            baseType.ToDisplayString()));
                        break;
                    }
                }
            }
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
