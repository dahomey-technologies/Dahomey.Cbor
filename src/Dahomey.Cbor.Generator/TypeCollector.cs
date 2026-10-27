using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Member = (string Name, string CborName, int? Index, Microsoft.CodeAnalysis.ITypeSymbol Type);

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

        /// <summary>
        /// The assembly the generated code is compiled into, which decides whether an <c>internal</c>
        /// accessor of a collected type is reachable. Null means unknown, and is taken as reachable.
        /// </summary>
        private readonly IAssemblySymbol? _accessingAssembly;

        private readonly Dictionary<string, TypeModel> _models =
            new Dictionary<string, TypeModel>();

        private readonly Func<ISymbol, bool> _isAccessible;
        private readonly Func<ITypeSymbol, ITypeSymbol, bool> _isIdentical;

        /// <param name="isAccessible">
        /// Whether the generated context can name a symbol -- asked of the compilation, because it is a
        /// property of the call site: <c>internal</c> means this assembly, and <c>protected</c> reaches
        /// a context nested in the type but not one nested in a subtype.
        /// </param>
        /// <param name="isIdentical">
        /// Whether two types are one type to the runtime, which is what <c>CreatorMapping</c> compares:
        /// tuple element names and nullable annotations are erased, symbol equality counts the former.
        /// </param>
        public TypeCollector(
            GenerationOptions options,
            List<DiagnosticInfo> diagnostics,
            Func<ISymbol, bool> isAccessible,
            Func<ITypeSymbol, ITypeSymbol, bool> isIdentical,
            IAssemblySymbol? accessingAssembly = null)
        {
            _options = options;
            _diagnostics = diagnostics;
            _isAccessible = isAccessible;
            _isIdentical = isIdentical;
            _accessingAssembly = accessingAssembly;
        }

        public void Collect(ITypeSymbol type)
        {
            string key = Key(type);

            if (_models.ContainsKey(key))
            {
                return;
            }

            TypeKind kind = Classify(type, out ITypeSymbol? element, out ITypeSymbol? value, out ITypeSymbol? underlying, out string? backing);

            TypeModel model = new TypeModel(type, kind)
            {
                ElementType = element,
                ValueType = value,
                UnderlyingType = underlying,
                BackingType = backing,
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
                case TypeKind.InterfaceCollection:
                    Collect(element!);
                    model.Dependencies.Add(element!);
                    break;

                case TypeKind.Dictionary:
                case TypeKind.InterfaceDictionary:
                    Collect(element!);
                    Collect(value!);
                    model.Dependencies.Add(element!);
                    model.Dependencies.Add(value!);
                    break;

                case TypeKind.Tuple:
                    CollectTuple((INamedTypeSymbol)type, model);
                    break;

                case TypeKind.Object:
                    CollectObject(type, model);
                    break;
            }
        }

        /// <summary>
        /// A tuple's type arguments, each of which needs a converter of its own before this one can be
        /// constructed.
        /// </summary>
        /// <remarks>
        /// For an arity past seven the eighth argument is the <c>Rest</c>, a tuple in its own right, so
        /// collecting it recurses and the chain terminates at seven or fewer. That is also why the
        /// dependency has to be recorded: <c>Tuple8Converter</c> asks the registry for the <c>Rest</c>'s
        /// converter while it is being constructed, so the <c>Rest</c> must be registered first.
        /// </remarks>
        private void CollectTuple(INamedTypeSymbol type, TypeModel model)
        {
            foreach (ITypeSymbol argument in type.TypeArguments)
            {
                model.TupleArguments.Add(argument);

                Collect(argument);
                model.Dependencies.Add(argument);
            }
        }

        /// <summary>
        /// Whether this is one of the <c>System.ValueTuple</c> types.
        /// </summary>
        /// <remarks>
        /// By metadata name rather than by <c>IsTupleType</c>, which is false for the one-element
        /// <c>ValueTuple&lt;T&gt;</c> -- C# has no tuple syntax for a single element -- and that arity is
        /// exactly what an eight-element tuple's <c>Rest</c> is.
        /// </remarks>
        private static bool IsValueTuple(INamedTypeSymbol type)
        {
            return type.MetadataName.StartsWith("ValueTuple`", System.StringComparison.Ordinal)
                && type.ContainingNamespace is { Name: "System" }
                && type.ContainingNamespace.ContainingNamespace is { IsGlobalNamespace: true };
        }

        /// <summary>
        /// Which converter a collection interface resolves to, and the concrete type it is built into
        /// while being read, mirroring <c>CollectionConverterProvider</c>'s choices exactly.
        /// </summary>
        /// <remarks>
        /// The backing type is what makes these constructible at all: an interface cannot be
        /// instantiated, so the read fills a concrete collection and hands it back as the interface.
        /// <c>List&lt;T&gt;</c> for the ordered interfaces and <c>HashSet&lt;T&gt;</c> for
        /// <c>ISet&lt;T&gt;</c> — the same pair the reflection path picks, because a document written
        /// through one path has to read back through the other.
        /// <para>
        /// <c>IReadOnlyList&lt;T&gt;</c> and <c>IReadOnlyCollection&lt;T&gt;</c> are included even
        /// though a caller cannot add to what they hand back: the converter builds a <c>List&lt;T&gt;</c>
        /// and returns it as the read-only interface, which is what the reflection path does.
        /// </para>
        /// </remarks>
        private static TypeKind ClassifyCollectionInterface(
            INamedTypeSymbol named,
            out ITypeSymbol? element,
            out ITypeSymbol? value,
            out string? backing)
        {
            element = null;
            value = null;
            backing = null;

            string constructed = named.ConstructedFrom.ToDisplayString();

            if (constructed == "System.Collections.Generic.IDictionary<TKey, TValue>"
                && named.TypeArguments.Length == 2)
            {
                element = named.TypeArguments[0];
                value = named.TypeArguments[1];
                return TypeKind.InterfaceDictionary;
            }

            if (named.TypeArguments.Length != 1)
            {
                return TypeKind.Unsupported;
            }

            switch (constructed)
            {
                case "System.Collections.Generic.ISet<T>":
                    element = named.TypeArguments[0];
                    backing = "global::System.Collections.Generic.HashSet";
                    return TypeKind.InterfaceCollection;

                case "System.Collections.Generic.IList<T>":
                case "System.Collections.Generic.ICollection<T>":
                case "System.Collections.Generic.IEnumerable<T>":
                case "System.Collections.Generic.IReadOnlyList<T>":
                case "System.Collections.Generic.IReadOnlyCollection<T>":
                    element = named.TypeArguments[0];
                    backing = "global::System.Collections.Generic.List";
                    return TypeKind.InterfaceCollection;

                default:
                    return TypeKind.Unsupported;
            }
        }

        /// <summary>
        /// A tuple's elements with the <c>Rest</c> chain flattened, which is what the wire form is and
        /// therefore what a schema has to describe.
        /// </summary>
        public static IEnumerable<ITypeSymbol> FlattenTupleElements(INamedTypeSymbol type)
        {
            ImmutableArray<ITypeSymbol> arguments = type.TypeArguments;

            for (int index = 0; index < arguments.Length; index++)
            {
                if (index == 7
                    && arguments[index] is INamedTypeSymbol rest
                    && IsValueTuple(rest))
                {
                    foreach (ITypeSymbol nested in FlattenTupleElements(rest))
                    {
                        yield return nested;
                    }

                    continue;
                }

                yield return arguments[index];
            }
        }

        private void CollectObject(ITypeSymbol type, TypeModel model)
        {
            model.CanInstantiate = type is { IsAbstract: false, TypeKind: Microsoft.CodeAnalysis.TypeKind.Class or Microsoft.CodeAnalysis.TypeKind.Struct }
                && HasAccessibleParameterlessConstructor(type);

            // An abstract base is never instantiated -- its subtypes are -- so only a concrete type
            // without a constructor is a problem. The reflection path can reach a non-public
            // constructor or a [CborConstructor] creator mapping; a generated factory is `new T()`.
            IMethodSymbol? declaredCreator = FindCreatorConstructor(type);
            IMethodSymbol? creator = declaredCreator;
            bool creatorUnreachable = false;

            // A creator the context cannot call would be emitted as a call that does not compile
            // (CS0122); it is reported and dropped instead.
            if (creator is not null && !_isAccessible(creator))
            {
                _diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.InaccessibleCreatorConstructor,
                    creator.Locations.FirstOrDefault(),
                    type.ToDisplayString(),
                    DescribeAccessibility(creator.DeclaredAccessibility)));
                creator = null;
                creatorUnreachable = true;
            }

            // A required member has to be set by an object initializer or by a constructor marked
            // [SetsRequiredMembers]; the generated creator and factory are plain calls (CS9035).
            string? requiredMember = type is { IsAbstract: false, TypeKind: Microsoft.CodeAnalysis.TypeKind.Class or Microsoft.CodeAnalysis.TypeKind.Struct }
                ? FirstRequiredMember(type)
                : null;

            if (requiredMember is not null)
            {
                if (creator is not null && !HasAttribute(creator, "SetsRequiredMembersAttribute"))
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.CreatorNotReproducible,
                        creator.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        RequiredMemberReason(requiredMember, "this constructor")));
                    // A parameterless creator is the very constructor the factory calls.
                    if (creator.Parameters.Length == 0)
                    {
                        model.CanInstantiate = false;
                    }

                    creator = null;
                    creatorUnreachable = true;
                }
            }

            if (!model.CanInstantiate
                && !creatorUnreachable
                && type is { IsAbstract: false, TypeKind: Microsoft.CodeAnalysis.TypeKind.Class }
                && creator is null)
            {
                _diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.NoParameterlessConstructor,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
            }

            ReadTypeLevelAttributes(type, model);
            ReportUnsupportedFeatures(type);

            Dictionary<string, string> membersByCborName = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<int, string> membersByCborIndex = new Dictionary<int, string>();
            List<Member> walked = new List<Member>();
            List<ISymbol> readOnlyMembers = new List<ISymbol>();

            foreach (ISymbol member in EnumerateMembers(type))
            {
                if (HasInheritedAttribute(member, "CborIgnoreAttribute"))
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
                        model.ObjectFormat,
                        model.ObjectFormatInheritedFrom is null
                            ? string.Empty
                            : $" (inherited from '{model.ObjectFormatInheritedFrom}')"));
                    continue;
                }

                // Reported once the creator is resolved: a member it supplies needs no setter.
                if (!canWrite)
                {
                    readOnlyMembers.Add(member);
                }

                // The wire key, which is the name for StringKeyMap and the index for the other two
                // formats -- the same split ObjectConverter's read lookup makes. Two members under one
                // key cannot both be in a document, so the second is reported.
                //
                // Reported and still registered, so that nothing here assumes the build stops. If
                // emission ever proceeds past the diagnostic, both members reach
                // ObjectMapping.ValidateMemberNamesAndindexes, which refuses the mapping exactly as it
                // does for the same source on the reflection path. Registering one member fewer than the
                // source declares would be silent instead.
                if (model.ObjectFormat == "StringKeyMap")
                {
                    if (membersByCborName.TryGetValue(cborName, out string? firstUnderName))
                    {
                        _diagnostics.Add(DiagnosticInfo.Create(
                            Diagnostics.DuplicateMemberName,
                            member.Locations.FirstOrDefault(),
                            type.Name,
                            firstUnderName,
                            member.Name,
                            cborName));
                    }
                    else
                    {
                        membersByCborName[cborName] = member.Name;
                    }
                }
                else
                {
                    if (membersByCborIndex.TryGetValue(cborIndex!.Value, out string? firstUnderIndex))
                    {
                        _diagnostics.Add(DiagnosticInfo.Create(
                            Diagnostics.DuplicateMemberIndex,
                            member.Locations.FirstOrDefault(),
                            type.Name,
                            firstUnderIndex,
                            member.Name,
                            cborIndex.Value.ToString(CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        membersByCborIndex[cborIndex.Value] = member.Name;
                    }
                }

                // Found once, so the name and the type it is cast to cannot come from different methods.
                IMethodSymbol? shouldSerialize = FindShouldSerializeMethod(
                    type, member, out IMethodSymbol? refused, out string? refusal);

                if (refused is not null && refusal is not null)
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.ShouldSerializeRefused,
                        refused.Locations.FirstOrDefault() ?? member.Locations.FirstOrDefault(),
                        type.Name,
                        member.Name,
                        refused.Name,
                        refusal));
                }

                model.Members.Add(new MemberModel(member.Name, cborName, cborIndex, memberType, canRead, canWrite)
                {
                    RequirementPolicy = ReadRequirementPolicy(member),
                    ShadowedOwner = ShadowedOwnerOf(type, member),
                    ShouldSerializeMethod = shouldSerialize?.Name,
                    ShouldSerializeOwner = shouldSerialize?.ContainingType
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                });

                walked.Add((member.Name, cborName, cborIndex, memberType));

                Collect(memberType);

                // A member's converter must exist before this object's converter is constructed —
                // except when it is this very type, which the self-reference break handles.
                if (Key(memberType) != Key(type))
                {
                    model.Dependencies.Add(memberType);
                }
            }

            bool isStruct = type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct;
            bool marked = creator is not null && HasAttribute(creator, "CborConstructorAttribute");
            HashSet<string> supplied = new HashSet<string>(StringComparer.Ordinal);
            bool resolved = creator is null || ResolveCreator(type, model, creator, walked, quiet: isStruct && !marked, supplied);
            bool creatorReported = creatorUnreachable || (!resolved && (!isStruct || marked));

            // A struct whose only constructor does not fill every member is read as it always was:
            // `new T()` and the setters. Built through the constructor, its unsupplied members would be
            // left to a setter lambda, which a struct member cannot take. The reflection path refuses
            // such a struct (NotSupportedException); the generated path stays lenient unless the
            // constructor is marked [CborConstructor], which asks for it and is reported.
            if (creator is not null && isStruct && (!resolved || walked.Any(w => !supplied.Contains(w.Name))))
            {
                if (resolved && marked)
                {
                    creatorReported = true;
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.CreatorNotReproducible,
                        creator.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        "it does not supply every member, and a struct member cannot be set after construction; "
                            + "the reflection path throws NotSupportedException when it reads one"));
                }

                model.CreatorParameters.Clear();
                supplied.Clear();
            }

            // The `new T()` factory is emitted only for a type without a creator, so it can only fail to
            // compile for one that ends up without. Judged here because a struct's creator may just
            // have been dropped; a marked parameterless creator was judged with the creators above.
            if (requiredMember is not null
                && model.CanInstantiate
                && model.CreatorParameters.Count == 0
                && declaredCreator is not { Parameters.Length: 0 })
            {
                IMethodSymbol? parameterless = (type as INamedTypeSymbol)?.InstanceConstructors
                    .FirstOrDefault(c => !c.IsStatic && c.Parameters.Length == 0);

                if (parameterless is null || !HasAttribute(parameterless, "SetsRequiredMembersAttribute"))
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.CreatorNotReproducible,
                        parameterless?.Locations.FirstOrDefault() ?? type.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        RequiredMemberReason(requiredMember, "`new T()`")));
                    model.CanInstantiate = false;
                }
            }

            foreach (ISymbol member in readOnlyMembers)
            {
                if (!creatorReported && !supplied.Contains(member.Name))
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.MemberNotDeserializable,
                        member.Locations.FirstOrDefault(),
                        type.Name,
                        member.Name));
                }
            }
        }

        private static string RequiredMemberReason(string member, string call)
        {
            return $"member '{member}' is required, which only an object initializer or a constructor marked "
                + $"[SetsRequiredMembers] can satisfy, and a generated context calls {call} without one";
        }

        /// <summary>
        /// Maps each creator parameter to the member it is filled from, as <c>CreatorMapping</c> does,
        /// collecting the C# names of the members that supply one. False when it cannot be reproduced.
        /// </summary>
        /// <remarks>
        /// <c>[CborConstructor(...)]</c> lists names under <c>StringKeyMap</c> and indexes under the
        /// other two, positionally; the other kind is ignored. Unlisted, a parameter is matched on the
        /// member's C# name (exact case, then ignoring it), then under <c>StringKeyMap</c> on its CBOR
        /// name. What the reflection path refuses when it builds the mapping -- a wrong count, a listed
        /// key matching nothing, a type mismatch -- is reported here, and so is a parameter matching
        /// nothing, which that path leaves at its default.
        /// </remarks>
        private bool ResolveCreator(
            ITypeSymbol type, TypeModel model, IMethodSymbol creator, List<Member> walked, bool quiet,
            HashSet<string> supplied)
        {
            bool byName = model.ObjectFormat == "StringKeyMap";
            (string[]? names, int[]? indexes) = ReadCreatorArguments(creator);
            bool listed = byName ? names is not null : indexes is not null;
            int listedCount = byName ? names?.Length ?? 0 : indexes?.Length ?? 0;
            const string RefusedAtBuild = "the reflection path refuses this mapping with a CborException when it is built";

            bool Refuse(string what)
            {
                if (!quiet)
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.CreatorNotReproducible, creator.Locations.FirstOrDefault(), type.ToDisplayString(), what));
                }

                model.CreatorParameters.Clear();
                return false;
            }

            if (listed && listedCount != creator.Parameters.Length)
            {
                return Refuse($"[CborConstructor] lists {listedCount} member " + (byName ? "names" : "indexes")
                    + $" for {creator.Parameters.Length} constructor parameters; {RefusedAtBuild}");
            }

            for (int i = 0; i < creator.Parameters.Length; i++)
            {
                IParameterSymbol parameter = creator.Parameters[i];

                if (parameter.RefKind != RefKind.None)
                {
                    // The call would omit the modifier (CS1620), and the reflection path refuses the
                    // mapping for any by-reference parameter -- `in` included, which compiles here.
                    return Refuse($"parameter '{parameter.Name}' is passed by reference ({parameter.RefKind.ToString().ToLowerInvariant()}); "
                        + RefusedAtBuild);
                }

                Member match = listed
                    ? byName
                        ? Find(walked, w => w.CborName, names![i])
                        : walked.FirstOrDefault(w => w.Index == indexes![i])
                    : Find(walked, w => w.Name, parameter.Name);

                if (match.Name is null && !listed && byName)
                {
                    match = Find(walked, w => w.CborName, parameter.Name);
                }

                if (match.Name is null)
                {
                    return Refuse(listed
                        ? $"[CborConstructor] lists member {(byName ? "name" : "index")} '{(byName ? names![i] : indexes![i].ToString(CultureInfo.InvariantCulture))}', which matches no serialized member; {RefusedAtBuild}"
                        : $"parameter '{parameter.Name}' matches no serialized member, so the reflection path would leave it at its default value; "
                            + "name it after a member, or list the members in [CborConstructor]");
                }

                if (!_isIdentical(match.Type, parameter.Type))
                {
                    // CreatorMapping compares the two types exactly and throws on a difference.
                    return Refuse($"parameter '{parameter.Name}' is a {parameter.Type.ToDisplayString()} but is filled from member "
                        + $"'{match.Name}', a {match.Type.ToDisplayString()}; {RefusedAtBuild}");
                }

                supplied.Add(match.Name);
                model.CreatorParameters.Add((FullNameOf(parameter.Type), match.CborName, match.Index));
            }

            return true;
        }

        /// <summary>The first match on the exact name, else on the name ignoring case, as <c>CreatorMapping</c> does.</summary>
        private static Member Find(List<Member> walked, Func<Member, string> key, string name)
        {
            foreach (StringComparison comparison in new[] { StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase })
            {
                foreach (Member candidate in walked)
                {
                    if (string.Equals(key(candidate), name, comparison))
                    {
                        return candidate;
                    }
                }
            }

            return default;
        }

        private static string? FirstRequiredMember(ITypeSymbol type)
        {
            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    {
                        return member.Name;
                    }
                }
            }

            return null;
        }

        private static (string[]? Names, int[]? Indexes) ReadCreatorArguments(IMethodSymbol creator)
        {
            AttributeData? attribute = creator.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "CborConstructorAttribute");

            if (attribute is null
                || attribute.ConstructorArguments.Length != 1
                || attribute.ConstructorArguments[0].Kind != TypedConstantKind.Array
                || attribute.ConstructorArguments[0].IsNull)
            {
                return (null, null);
            }

            TypedConstant argument = attribute.ConstructorArguments[0];
            ITypeSymbol? element = (argument.Type as IArrayTypeSymbol)?.ElementType;

            if (element?.SpecialType == SpecialType.System_String)
            {
                return (argument.Values.Select(v => (string)v.Value!).ToArray(), null);
            }

            if (element?.SpecialType == SpecialType.System_Int32)
            {
                return (null, argument.Values.Select(v => (int)v.Value!).ToArray());
            }

            return (null, null);
        }

        private static string DescribeAccessibility(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Private => "a private",
                Accessibility.Protected => "a protected",
                Accessibility.Internal => "an internal",
                Accessibility.ProtectedOrInternal => "a protected internal",
                Accessibility.ProtectedAndInternal => "a private protected",
                _ => "an inaccessible",
            };
        }

        private static string FullNameOf(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
            ("CborNamingConventionAttribute", "[CborNamingConvention]"),
            ("CborLengthModeAttribute", "[CborLengthMode]"),
        };

        private static readonly (string Attribute, string Description)[] UnsupportedMemberFeatures =
        {
            ("CborConverterAttribute", "[CborConverter]"),
            ("CborIgnoreIfDefaultAttribute", "[CborIgnoreIfDefault]"),
            ("CborLengthModeAttribute", "[CborLengthMode]"),
            ("DefaultValueAttribute", "[DefaultValue]"),
        };

        private void ReportUnsupportedFeatures(ITypeSymbol type)
        {
            foreach ((string attribute, string description) in UnsupportedTypeFeatures)
            {
                // [CborNamingConvention] and [CborLengthMode] are inherited, as reflection reads them
                // (GetCustomAttribute): a base's governs its subtypes there, so a subtype of one is as
                // unsupported as the base. [CborConverter] is inheritable too, but a converter is written
                // for one type, so what reflection does with a subtype of one is not something to
                // reproduce; it is left as it was.
                bool inheritable = attribute is "CborNamingConventionAttribute" or "CborLengthModeAttribute";
                ITypeSymbol? declaredOn = type;

                if (inheritable
                    ? NearestAttribute(type, attribute, out declaredOn) is not null
                    : HasAttribute(type, attribute))
                {
                    Report(
                        type.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        SymbolEqualityComparer.Default.Equals(declaredOn, type)
                            ? description
                            : $"{description} (inherited from {declaredOn!.ToDisplayString()})");
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
        /// Members in the order the reflection path sees them: every property, then every field, each
        /// most-derived type first, then each base (<c>DefaultObjectMappingConvention</c> maps the
        /// result of <c>GetProperties</c>, then that of <c>GetFields</c>). Matching that order matters
        /// because it determines the order members appear on the wire, and the generated output is
        /// required to be byte-identical.
        /// </summary>
        /// <remarks>
        /// An overridden property is declared on both types and is one member, so the base declaration
        /// is dropped once an override for it has been seen — the walk runs most-derived first, so the
        /// override always comes first. <c>Type.GetProperties</c> collapses an override the same way,
        /// which is what makes this a match for the reflection path rather than a preference: yielding
        /// both put two mappings under one name, so the generated context wrote the key twice and, since
        /// the mapping validation added in #186, threw while being built.
        /// <para>
        /// A property hiding a base property with <c>new</c> is folded only as <c>GetProperties</c>
        /// folds it: the base one is dropped when the signatures are identical, whatever the hiding
        /// property's own accessibility, getter or attributes. One of a different type hides nothing,
        /// both are returned, and the reflection path sees two members under one name and refuses the
        /// mapping; the generator reports <c>CBOR1013</c> for the same type, which is the same answer
        /// given earlier. Fields hide nothing at all.
        /// </para>
        /// </remarks>
        private IEnumerable<ISymbol> EnumerateMembers(ITypeSymbol type)
        {
            return EnumerateMembers<IPropertySymbol>(type).Concat(EnumerateMembers<IFieldSymbol>(type));
        }

        private IEnumerable<ISymbol> EnumerateMembers<TMember>(ITypeSymbol type)
            where TMember : ISymbol
        {
            HashSet<ISymbol> overriddenByADerivedMember =
                new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            // Properties declared by the types walked so far, which hide a base property that matches.
            List<IPropertySymbol> declaredNearer = new List<IPropertySymbol>();

            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                List<IPropertySymbol> declaredHere = new List<IPropertySymbol>();

                foreach (ISymbol member in current.GetMembers().OfType<TMember>())
                {

                    if (overriddenByADerivedMember.Contains(member))
                    {
                        continue;
                    }

                    if (member is IPropertySymbol { IsOverride: true } overriding)
                    {
                        for (IPropertySymbol? overridden = overriding.OverriddenProperty;
                             overridden is not null;
                             overridden = overridden.OverriddenProperty)
                        {
                            overriddenByADerivedMember.Add(overridden);
                        }
                    }

                    // Whatever the nearer property is -- skipped for its getter, ignored, not even
                    // readable -- it still hides: GetProperties decides on the signature alone.
                    if (member is IPropertySymbol { IsIndexer: false } property)
                    {
                        declaredHere.Add(property);

                        if (declaredNearer.Any(nearer => HidesByNameAndSignature(nearer, property)))
                        {
                            continue;
                        }
                    }

                    if (member.DeclaredAccessibility == Accessibility.Public)
                    {
                        // A public property can still have a getter that is not: the reflection path
                        // decides on the getter, so the property is skipped there unless [CborProperty]
                        // asks for it, and emitting `o.Name` for one is CS0271 in the generated code.
                        if (member is IPropertySymbol { GetMethod: { DeclaredAccessibility: not Accessibility.Public } getter })
                        {
                            if (!IsSerializedByTheReflectionPath(member))
                            {
                                continue;
                            }

                            if (!IsReachableFromTheContext(getter))
                            {
                                _diagnostics.Add(DiagnosticInfo.Create(
                                    Diagnostics.NonPublicMember,
                                    member.Locations.FirstOrDefault(),
                                    type.Name,
                                    member.Name,
                                    $"public but has a {AccessibilityName(getter.DeclaredAccessibility)} getter"));
                                continue;
                            }
                        }

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
                            AccessibilityName(member.DeclaredAccessibility)));
                    }
                }

                declaredNearer.AddRange(declaredHere);
            }
        }

        /// <summary>
        /// Whether <c>GetProperties</c> drops <paramref name="hidden"/> because <paramref name="nearer"/>
        /// hides it: the same name and an identical signature as declared. A type that differs (a
        /// <c>new int</c> over a <c>string</c>) hides nothing, and both are returned; so does a type
        /// that is the same only once a base's type argument is substituted, because the comparison is
        /// of the declarations (<c>GenericBase&lt;T&gt;.Value</c> is a <c>T</c>, not an <c>int</c>).
        /// </summary>
        private static bool HidesByNameAndSignature(IPropertySymbol nearer, IPropertySymbol hidden)
        {
            return nearer.Name == hidden.Name
                && SymbolEqualityComparer.Default.Equals(
                    nearer.OriginalDefinition.Type, hidden.OriginalDefinition.Type);
        }

        /// <summary>
        /// The declaring type of <paramref name="member"/> when a type nearer to <paramref name="type"/>
        /// declares a property or field of the same name, so that the generated accessor has to name the
        /// declaring type; otherwise null.
        /// </summary>
        private static string? ShadowedOwnerOf(ITypeSymbol type, ISymbol member)
        {
            for (ITypeSymbol? current = type;
                 current is not null && !SymbolEqualityComparer.Default.Equals(current, member.ContainingType);
                 current = current.BaseType)
            {
                if (current.GetMembers(member.Name).Any(m => m is IPropertySymbol or IFieldSymbol))
                {
                    return Key(member.ContainingType);
                }
            }

            return null;
        }

        /// <summary>
        /// Whether <c>DefaultObjectMappingConvention</c> would serialize this member whose accessibility
        /// is not plain public, and therefore whether dropping it changes the bytes.
        /// </summary>
        /// <remarks>
        /// That convention excludes a property by its <b>getter</b> -- protected, private or static --
        /// and a field that is private or static, unless the member carries <c>[CborProperty]</c>, which
        /// overrides the filter entirely. Everything else is serialized: an internal, protected internal
        /// or <c>private protected</c> getter, and a protected, internal, protected internal or
        /// <c>private protected</c> field. Only <c>IsFamily</c> and <c>IsPrivate</c> are tested, so the
        /// two intersection forms (<c>private protected</c>) are not excluded -- they are neither.
        /// A property with no getter is skipped outright, attribute or not.
        /// </remarks>
        private static bool IsSerializedByTheReflectionPath(ISymbol member)
        {
            if (member.IsStatic)
            {
                return false;
            }

            if (HasInheritedAttribute(member, "CborIgnoreAttribute"))
            {
                return false;
            }

            if (member is IPropertySymbol { GetMethod: null })
            {
                return false;
            }

            if (HasInheritedAttribute(member, "CborPropertyAttribute"))
            {
                return true;
            }

            return member switch
            {
                IPropertySymbol property => property.GetMethod!.DeclaredAccessibility
                    is not (Accessibility.Private or Accessibility.Protected),
                IFieldSymbol field => field.DeclaredAccessibility is not Accessibility.Private,
                _ => false,
            };
        }

        /// <summary>
        /// Whether generated code in the context's assembly can call this accessor. An
        /// <c>internal</c> one is reachable only from its own assembly or one it names with
        /// <c>InternalsVisibleTo</c>, which is what makes a getter on a base type read from metadata
        /// the case that matters.
        /// </summary>
        private bool IsReachableFromTheContext(ISymbol accessor)
        {
            return accessor.DeclaredAccessibility switch
            {
                Accessibility.Public => true,
                Accessibility.Internal or Accessibility.ProtectedOrInternal =>
                    _accessingAssembly is null
                    || accessor.ContainingAssembly.GivesAccessTo(_accessingAssembly),
                _ => false,
            };
        }

        private static string AccessibilityName(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.ProtectedOrInternal => "protected internal",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => accessibility.ToString().ToLowerInvariant(),
            };
        }

        private void ReadTypeLevelAttributes(ITypeSymbol type, TypeModel model)
        {
            model.ObjectFormat = _options.ObjectFormat;
            model.DiscriminatorPolicy = _options.DiscriminatorPolicy;

            AttributeData? objectFormat = NearestAttribute(
                type, "CborObjectFormatAttribute", out ITypeSymbol? formatDeclaredOn,
                attribute => attribute.ConstructorArguments.Length == 1);

            if (objectFormat is not null)
            {
                model.ObjectFormatDeclared = true;
                model.ObjectFormatInheritedFrom =
                    SymbolEqualityComparer.Default.Equals(formatDeclaredOn, type)
                        ? null
                        : formatDeclaredOn!.ToDisplayString();
                model.ObjectFormat = objectFormat.ConstructorArguments[0].Value switch
                {
                    1 => "IntKeyMap",
                    2 => "Array",
                    _ => "StringKeyMap",
                };
            }

            // Read the way reflection reads them: GetCustomAttribute<T>() inherits, so an undecorated
            // subtype takes its base's discriminator, and a base with one kind and a subtype with the
            // other is the both-attributes conflict reflection throws for.
            AttributeData? stringDiscriminator =
                NearestAttribute(type, "CborDiscriminatorAttribute", out _);
            AttributeData? intDiscriminator =
                NearestAttribute(type, "CborIntDiscriminatorAttribute", out _);

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
            foreach (AttributeData attribute in WithOverriddenDeclarations(member)
                .SelectMany(declaration => declaration.GetAttributes()))
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
            out ITypeSymbol? underlying,
            out string? backing)
        {
            element = null;
            value = null;
            underlying = null;
            backing = null;

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

                // Before the collection tests: a tuple implements none of those interfaces, but it is
                // the more specific shape and reads better stated first.
                if (IsValueTuple(named))
                {
                    return TypeKind.Tuple;
                }

                // The collection interfaces, before the tests below: IList<T> implements ICollection<T>,
                // so it would otherwise be taken for a concrete collection and then refused for having
                // no parameterless constructor -- which is exactly the CBOR1002 this replaces.
                if (named.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface)
                {
                    TypeKind interfaceKind = ClassifyCollectionInterface(named, out element, out value, out backing);

                    if (interfaceKind != TypeKind.Unsupported)
                    {
                        return interfaceKind;
                    }
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
            return type.SpecialType == SpecialType.System_Object;
        }

        private bool IsPrimitive(ITypeSymbol type)
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

            // MATCHED PAIR: every case below duplicates a row of
            // src/Dahomey.Cbor/Serialization/Converters/Providers/PrimitiveConverterProvider.cs. It
            // cannot be shared -- the generator is an analyzer assembly and must not reference the
            // runtime library -- so the two must be edited together. A type this list omits while the
            // provider resolves it is collected as an object, and a generated context then writes its
            // properties where the reflection path writes a scalar: a green build and the wrong bytes.
            // A type this list names while the provider does not is worse, because nothing catches it
            // until run time. PrimitiveConverterProviderParityTests is what holds the two in step.
            //
            // None of these has a SpecialType, so each is matched by name.
            switch (type.ToDisplayString())
            {
                case "System.Numerics.BigInteger":
                case "Dahomey.Cbor.CborDecimalFraction":
                case "Dahomey.Cbor.CborBigFloat":
                case "System.Guid":
                // Reachable only from a target where these exist at all; on netstandard2.0 the
                // library compiles no converter for them and the consumer has no type to name.
                case "System.DateOnly":
                case "System.TimeOnly":
                case "System.DateTimeOffset":
                case "System.Half":
                    return true;

                // The only classification that depends on an option. Under the default there is no
                // TimeSpanConverter to resolve, so the type is collected as an object -- which is what
                // the reflection path does with it too, and the corpus compares the two.
                case "System.TimeSpan":
                    return _options.WritesDurations;
            }

            // System.Object is deliberately absent.
            // PrimitiveConverterProvider has no case for it, so at run time it falls through to
            // ObjectConverterProvider and reaches MakeGenericType -- the exact failure a generated
            // context exists to prevent, and one the AOT analyzer cannot see either. Classifying it
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

        private bool HasAccessibleParameterlessConstructor(ITypeSymbol type)
        {
            if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct)
            {
                return true;
            }

            return type is INamedTypeSymbol named
                && named.InstanceConstructors.Any(c =>
                    !c.IsStatic && c.Parameters.Length == 0 && _isAccessible(c));
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

        /// <summary>
        /// The attribute <c>type.GetCustomAttribute&lt;T&gt;()</c> would return: the nearest declaration
        /// on the type or any base class.
        /// </summary>
        /// <remarks>
        /// Every type-level Cbor attribute is inheritable (none sets <c>Inherited = false</c>) and the
        /// reflection path reads them all with <c>GetCustomAttribute</c>, which inherits. But
        /// <c>ISymbol.GetAttributes()</c> returns only what the symbol itself declares, so reading it
        /// directly treats a subtype of a decorated base as undecorated -- a silent difference in the
        /// bytes, or in what is refused. One helper for all of them keeps the rule in one place.
        /// </remarks>
        private static AttributeData? NearestAttribute(
            ITypeSymbol type,
            string attributeName,
            out ITypeSymbol? declaredOn,
            System.Func<AttributeData, bool>? accept = null)
        {
            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                foreach (AttributeData attribute in current.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name == attributeName && (accept is null || accept(attribute)))
                    {
                        declaredOn = current;
                        return attribute;
                    }
                }
            }

            declaredOn = null;
            return null;
        }

        private static bool HasAttribute(ISymbol symbol, string attributeName)
        {
            return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
        }

        /// <summary>
        /// The same, over every declaration an attribute lookup on this member reaches: the member
        /// itself, then the declaration it overrides, and so on to the base of the chain.
        /// </summary>
        /// <remarks>
        /// The reflection path reads member attributes <em>with inheritance</em> — both
        /// <c>MemberInfo.GetCustomAttribute&lt;T&gt;()</c> and <c>IsDefined(Type)</c> are the
        /// <c>inherit: true</c> overloads — while <c>ISymbol.GetAttributes()</c> returns only what the
        /// symbol itself declares. That difference is invisible until an override is collapsed onto its
        /// most-derived declaration, which <see cref="EnumerateMembers"/> does to match
        /// <c>Type.GetProperties</c>: the base declaration then goes away carrying the attributes the
        /// member is mapped by, and a <c>[CborProperty("id")]</c> on a virtual base writes the key
        /// <c>Id</c> from the generated path and <c>id</c> from the reflection path, with nothing said.
        /// <para>
        /// Nearest declaration first, so an override redeclaring the attribute wins, which is what
        /// reflection does for an attribute that is not <c>AllowMultiple</c>. A field has no chain.
        /// </para>
        /// </remarks>
        private static IEnumerable<ISymbol> WithOverriddenDeclarations(ISymbol member)
        {
            yield return member;

            for (IPropertySymbol? overridden = (member as IPropertySymbol)?.OverriddenProperty;
                 overridden is not null;
                 overridden = overridden.OverriddenProperty)
            {
                yield return overridden;
            }
        }

        /// <summary>
        /// The constructor a read builds this type through, or null where <c>new T()</c> will do:
        /// a <c>[CborConstructor]</c>, else the only constructor of a type with no parameterless one --
        /// a positional record's. Where several are declared, none marked and none parameterless, the
        /// reflection path takes the first in metadata order, which nothing guarantees Roslyn matches,
        /// so that case is left to CBOR1010, whose remedy settles it for both paths.
        /// </summary>
        private IMethodSymbol? FindCreatorConstructor(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named || named.IsAbstract)
            {
                return null;
            }

            // Every accessibility, as the reflection path binds NonPublic, so that one the context
            // cannot call is reported by the caller. Implicit ones are excluded (a record's copy
            // constructor); the primary constructor is not implicit.
            List<IMethodSymbol> constructors = named.InstanceConstructors
                .Where(c => !c.IsStatic && !c.IsImplicitlyDeclared)
                .ToList();

            IMethodSymbol? marked = constructors.FirstOrDefault(
                c => HasAttribute(c, "CborConstructorAttribute"));

            if (marked is not null)
            {
                return marked;
            }

            if (constructors.Any(c => c.Parameters.Length == 0))
            {
                return null;
            }

            // A record may declare its copy constructor itself (`private R(R original)`). It is never
            // the primary one, which the reflection path takes. In a class it is an ordinary constructor.
            if (named.IsRecord)
            {
                constructors = constructors
                    .Where(c => !(c.Parameters.Length == 1
                        && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, named)))
                    .ToList();
            }

            return constructors.Count == 1 ? constructors[0] : null;
        }

        /// <summary>
        /// The <c>[CborRequired]</c> policy for a member, as the <c>RequirementPolicy</c> member name to
        /// emit, or null where the member is not required.
        /// </summary>
        /// <remarks>
        /// The attribute's constructor defaults to <c>Always</c>, so an argumentless <c>[CborRequired]</c>
        /// means <c>Always</c> — the same default <c>CborRequiredAttribute</c> itself applies, rather
        /// than a second copy of the policy living here.
        /// <para>
        /// The value is rendered by <c>TypedConstant.ToCSharpString()</c> rather than mapped from its
        /// underlying number, so a reordering of <c>RequirementPolicy</c> cannot silently turn one policy
        /// into another here — the emitted text names the member the caller wrote.
        /// </para>
        /// </remarks>
        private static string? ReadRequirementPolicy(ISymbol member)
        {
            foreach (ISymbol declaration in WithOverriddenDeclarations(member))
            {
                foreach (AttributeData attribute in declaration.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name != "CborRequiredAttribute")
                    {
                        continue;
                    }

                    // The constructor argument first, or its default; then the named Policy, because
                    // reflection applies the property after the constructor has run, so a named value
                    // wins when both are written.
                    string policy = DefaultRequirementPolicy;

                    foreach (TypedConstant argument in attribute.ConstructorArguments)
                    {
                        policy = RenderEnumConstant(argument) ?? DefaultRequirementPolicy;
                        break;
                    }

                    foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
                    {
                        if (named.Key == "Policy")
                        {
                            policy = RenderEnumConstant(named.Value) ?? policy;
                        }
                    }

                    return policy;
                }
            }

            return null;
        }

        private const string DefaultRequirementPolicy =
            "global::Dahomey.Cbor.Attributes.RequirementPolicy.Always";

        /// <summary>
        /// An enum-valued attribute argument as the fully qualified member the caller wrote.
        /// </summary>
        /// <remarks>
        /// Resolved from the enum's own symbol rather than mapped from the underlying number, so a
        /// reordering of the enum cannot silently turn one member into another here.
        /// </remarks>
        private static string? RenderEnumConstant(TypedConstant constant)
        {
            if (constant.Type is not INamedTypeSymbol enumType || constant.Value is null)
            {
                return null;
            }

            foreach (ISymbol candidate in enumType.GetMembers())
            {
                if (candidate is IFieldSymbol { HasConstantValue: true } field
                    && Equals(field.ConstantValue, constant.Value))
                {
                    return enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        + "." + field.Name;
                }
            }

            // A value the enum does not name, such as (RequirementPolicy)7: reflection keeps the raw
            // number, so it is cast through rather than folded onto a member it was not written as.
            long raw = System.Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture);
            string literal = raw < 0
                ? "(" + raw.ToString(CultureInfo.InvariantCulture) + ")"
                : raw.ToString(CultureInfo.InvariantCulture);

            return "(" + enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ")" + literal;
        }

        /// <summary>
        /// The <c>ShouldSerialize&lt;Member&gt;()</c> method the reflection path would use for this
        /// member, or null where there is none -- including every member of a struct. When the
        /// reflection path would instead <b>throw</b> building the mapping, <paramref name="refused"/>
        /// is the method it chokes on and <paramref name="refusal"/> says why.
        /// </summary>
        /// <remarks>
        /// <c>DefaultObjectMappingConvention.ProcessShouldSerializeMethod</c> calls
        /// <c>GetMethod(name, no parameters)</c> on the member's <b>declaring</b> type, so the search
        /// starts there and goes up its bases, never down: a predicate declared only on a derived type
        /// is invisible to a member a base declares, and one hiding the base's with <c>new</c> loses to
        /// it. That lookup sees public methods only, so a non-public one is skipped rather than ending
        /// the search, and it is not <c>FlattenHierarchy</c>, so a static method inherited from a base
        /// is skipped too. At the level that has candidates, the outcome is:
        /// <list type="bullet">
        /// <item>two public candidates (<c>M()</c> and <c>M&lt;T&gt;()</c>): <c>AmbiguousMatchException</c>;</item>
        /// <item>one that does not return <c>bool</c>: ignored, whatever else it is;</item>
        /// <item>a <c>bool</c> one that is static (only possible on the declaring type) or generic: the
        /// expression tree cannot be built, and an <c>ArgumentException</c> escapes;</item>
        /// <item>otherwise it is the predicate.</item>
        /// </list>
        /// Each throw is reported rather than ignored: the generated path would otherwise write the
        /// member for a type the reflection path refuses outright. A struct's member converter never
        /// consults a predicate, but the convention still builds the lambda, so a struct is refused
        /// for the same shapes and simply never emits one.
        /// </remarks>
        private static IMethodSymbol? FindShouldSerializeMethod(
            ITypeSymbol type, ISymbol member, out IMethodSymbol? refused, out string? refusal)
        {
            refused = null;
            refusal = null;

            string name = "ShouldSerialize" + member.Name;
            ITypeSymbol? declaring = member.ContainingType;

            for (ITypeSymbol? current = declaring;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                bool onDeclaringType = SymbolEqualityComparer.Default.Equals(current, declaring);

                List<IMethodSymbol> candidates = current.GetMembers(name)
                    .OfType<IMethodSymbol>()
                    .Where(m => m.MethodKind == MethodKind.Ordinary
                        && m.Parameters.Length == 0
                        && m.DeclaredAccessibility == Accessibility.Public
                        && (onDeclaringType || !m.IsStatic))
                    .ToList();

                if (candidates.Count == 0)
                {
                    continue;
                }

                if (candidates.Count > 1)
                {
                    refused = candidates[0];
                    refusal = "it declares both a generic and a non-generic version, which makes the lookup ambiguous";
                    return null;
                }

                IMethodSymbol method = candidates[0];

                if (method.ReturnType.SpecialType != SpecialType.System_Boolean)
                {
                    return null;
                }

                if (method.IsStatic || method.IsGenericMethod)
                {
                    refused = method;
                    refusal = method.IsStatic
                        ? "it is static, and an instance call cannot be built over a static method"
                        : "it is generic, and a call cannot be built over an open generic method";
                    return null;
                }

                return type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct ? null : method;
            }

            return null;
        }

        private static bool HasInheritedAttribute(ISymbol member, string attributeName)
        {
            return WithOverriddenDeclarations(member)
                .Any(declaration => HasAttribute(declaration, attributeName));
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
