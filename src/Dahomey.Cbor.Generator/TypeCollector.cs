using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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

        private readonly Func<ISymbol, bool> _isAccessible;
        private readonly Func<ITypeSymbol, ITypeSymbol, bool> _isIdentical;
        private readonly Func<SyntaxTree, SemanticModel> _semanticModelFor;

        /// <param name="isAccessible">
        /// Whether a symbol can be named from the generated context -- the one place generated code
        /// calls a constructor from. Accessibility is a property of the call site, not of the symbol,
        /// so it is asked of the compilation rather than inferred from <c>DeclaredAccessibility</c>:
        /// that cannot tell an <c>internal</c> constructor in this assembly from one in another, or a
        /// <c>protected</c> one from a context nested in the type.
        /// </param>
        /// <param name="isIdentical">
        /// Whether two types are the same type to the runtime. <c>CreatorMapping</c> compares
        /// <c>System.Type</c>s, so differences the runtime erases do not count: tuple element names,
        /// <c>dynamic</c> against <c>object</c>, nullable reference annotations. Symbol equality counts
        /// the first two, and an identity conversion does not.
        /// </param>
        /// <param name="semanticModelFor">
        /// The semantic model of a syntax tree, for finding where a constructor parameter is read.
        /// </param>
        public TypeCollector(
            GenerationOptions options,
            List<DiagnosticInfo> diagnostics,
            Func<ISymbol, bool> isAccessible,
            Func<ITypeSymbol, ITypeSymbol, bool> isIdentical,
            Func<SyntaxTree, SemanticModel> semanticModelFor)
        {
            _semanticModelFor = semanticModelFor;
            _options = options;
            _diagnostics = diagnostics;
            _isAccessible = isAccessible;
            _isIdentical = isIdentical;
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

            // A creator the generated context cannot call would be emitted as a call that does not
            // compile (CS0122) -- the generator's contract is a diagnostic instead. Dropped after
            // reporting, so nothing below emits a call to it.
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
            // [SetsRequiredMembers]; the generated factory and creator are plain calls (CS9035). The
            // same members make the reflection path's construction fine, so refusing is the only way
            // to keep the build from failing in generated code with no mention of the generator.
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
                        $"member '{requiredMember}' is required, which only an object initializer or a constructor marked "
                            + "[SetsRequiredMembers] can satisfy, and a generated context calls this constructor without one"));
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

            // A struct is always "instantiable" (`new T()` exists), so several unmarked constructors
            // would otherwise be settled silently as default-plus-setters while the reflection path
            // takes the first one. That is only harmless when every member can be set afterwards, so
            // it is reported once the members are known and one of them cannot.
            bool ambiguousStruct = creator is null
                && !creatorUnreachable
                && type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct
                && HasSeveralUnmarkedConstructors(type);

            ReadTypeLevelAttributes(type, model);
            ReportUnsupportedFeatures(type);

            Dictionary<string, string> membersByCborName = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<int, string> membersByCborIndex = new Dictionary<int, string>();
            List<WalkedMember> walked = new List<WalkedMember>();
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
                bool initOnly = false;

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
                        initOnly = property.SetMethod is { IsInitOnly: true }
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

                // A member with no setter is reported once the creator has been resolved: a member the
                // creator supplies is read back through the constructor, so it needs no setter -- which
                // is what makes an `init`-only or get-only member work at all.
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
                    ShouldSerializeMethod = shouldSerialize?.Name,
                    ShouldSerializeOwner = shouldSerialize?.ContainingType
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                });

                walked.Add(new WalkedMember(member, member.Name, cborName, cborIndex, memberType, canWrite, initOnly));

                Collect(memberType);

                // A member's converter must exist before this object's converter is constructed —
                // except when it is this very type, which the self-reference break handles.
                if (Key(memberType) != Key(type))
                {
                    model.Dependencies.Add(memberType);
                }
            }

            // Resolved after the member walk, because a parameter is named for the member it fills and
            // that member's CBOR name is only settled once its attributes and the naming convention
            // have been read.
            CreatorResolution resolution = creator is not null
                ? ResolveCreator(type, model, creator, walked, quiet: type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct)
                : CreatorResolution.None;

            // A struct that a creator does not fill completely is read as it was before creators
            // existed: `new T()` and the setters. Building it through the constructor instead would
            // leave the unsupplied members to a setter lambda, which a struct member cannot take.
            // Quiet because that is what the generated path has always done for such a struct -- the
            // reflection path refuses it (NotSupportedException) and this stays lenient.
            if (creator is not null
                && type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct
                && (resolution.Failed || walked.Any(w => !resolution.Supplied.Contains(w.Name))))
            {
                model.CreatorParameters.Clear();
                model.CreatorMatchesByName = false;
                resolution = CreatorResolution.None;
            }

            // The `new T()` factory is emitted only for a type with no creator -- a read that has a
            // creator never calls it -- so it can only fail to compile for one that ends up without.
            // Judged here, after the creator is resolved, because a struct's creator may have just
            // been dropped. A marked parameterless creator was judged with the creators above.
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
                        $"member '{requiredMember}' is required, which only an object initializer or a constructor marked "
                            + "[SetsRequiredMembers] can satisfy, and a generated context calls `new T()` without one"));
                    model.CanInstantiate = false;
                }
            }

            if (ambiguousStruct && readOnlyMembers.Count > 0)
            {
                _diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.NoParameterlessConstructor,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString()));
            }

            foreach (ISymbol member in readOnlyMembers)
            {
                if (resolution.Supplied.Contains(member.Name))
                {
                    continue;
                }

                // A creator that is already an error -- unreachable, or refused -- gets the remedy
                // for it; "add a setter or a creator" on the members it would have supplied would
                // point away from that.
                IMethodSymbol? failedCreator = creatorUnreachable ? declaredCreator : resolution.Failed ? creator : null;

                if (failedCreator is not null)
                {
                    WalkedMember? serialized = walked
                        .Where(w => w.Name == member.Name)
                        .Select(w => (WalkedMember?)w)
                        .FirstOrDefault();

                    if (failedCreator.Parameters.Any(
                        p => string.Equals(p.Name, member.Name, StringComparison.OrdinalIgnoreCase)
                            || (serialized is not null
                                && string.Equals(p.Name, serialized.Value.CborName, StringComparison.OrdinalIgnoreCase))))
                    {
                        continue;
                    }
                }

                _diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.MemberNotDeserializable,
                    member.Locations.FirstOrDefault(),
                    type.Name,
                    member.Name));
            }
        }

        private readonly struct CreatorResolution
        {
            public static readonly CreatorResolution None =
                new CreatorResolution(new HashSet<string>(StringComparer.Ordinal), failed: false);

            public CreatorResolution(HashSet<string> supplied, bool failed)
            {
                Supplied = supplied;
                Failed = failed;
            }

            /// <summary>C# names of the members that take a value from a constructor parameter.</summary>
            public HashSet<string> Supplied { get; }

            /// <summary>The creator cannot be reproduced; the reason has been reported unless quiet.</summary>
            public bool Failed { get; }
        }

        private readonly struct WalkedMember
        {
            public WalkedMember(
                ISymbol symbol, string name, string cborName, int? index, ITypeSymbol type, bool canWrite, bool initOnly)
            {
                Symbol = symbol;
                Name = name;
                CborName = cborName;
                Index = index;
                Type = type;
                CanWrite = canWrite;
                InitOnly = initOnly;
            }

            public ISymbol Symbol { get; }
            public string Name { get; }
            public string CborName { get; }
            public int? Index { get; }
            public ITypeSymbol Type { get; }

            /// <summary>A public, non-<c>init</c> setter or a non-readonly field.</summary>
            public bool CanWrite { get; }

            /// <summary>A public <c>init</c> accessor, which only a constructor or initializer can call.</summary>
            public bool InitOnly { get; }
        }

        /// <summary>
        /// Maps each creator parameter to the member it is filled from, exactly as
        /// <c>CreatorMapping</c> does, and reports the C# names of the members that supply one.
        /// </summary>
        /// <remarks>
        /// <c>[CborConstructor(...)]</c> arguments are honoured: names for <c>StringKeyMap</c>, indexes
        /// for the other two, positionally against the constructor's parameters -- and the other kind
        /// is ignored, as the reflection path ignores it. Without arguments a parameter is matched
        /// case-insensitively by the member's CBOR name under <c>StringKeyMap</c> and by its C# name
        /// under the other two, which is what <c>CreatorMapping</c> compares.
        /// <para>
        /// A shape the reflection path refuses when the mapping is built (wrong count, a listed key
        /// that matches nothing, a parameter of another type than its member) is reported. So is an
        /// unlisted parameter that matches no member, which that path leaves at its default.
        /// </para>
        /// <para>
        /// Under <c>StringKeyMap</c> an unlisted parameter whose CBOR name matches nothing may still
        /// share its C# name with a member -- a naming convention or <c>[CborProperty("x")]</c> renamed
        /// it -- and the reflection path then leaves the parameter at its default and lets the member's
        /// setter supply the value afterwards. A member with a setter is handled the same way here,
        /// by not naming the parameter at all (the runtime's own matching then does what reflection
        /// does). An <c>init</c>-only member has no setter a generated context can call, so its value
        /// is passed through the parameter under the member's real key instead: the object ends up
        /// the same, and only a constructor that inspects the argument would see the difference. A
        /// member with no setter at all loses its value on the reflection path, which is reported.
        /// </para>
        /// </remarks>
        private CreatorResolution ResolveCreator(
            ITypeSymbol type, TypeModel model, IMethodSymbol creator, List<WalkedMember> walked, bool quiet)
        {
            HashSet<string> supplied = new HashSet<string>(StringComparer.Ordinal);
            bool byName = model.ObjectFormat == "StringKeyMap";
            (string[]? names, int[]? indexes) = ReadCreatorArguments(creator);
            bool hasListedKeys = byName ? names is not null : indexes is not null;
            int listedCount = byName ? names?.Length ?? 0 : indexes?.Length ?? 0;

            CreatorResolution Refuse(string what)
            {
                if (!quiet)
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.CreatorNotReproducible,
                        creator.Locations.FirstOrDefault(),
                        type.ToDisplayString(),
                        what));
                }

                model.CreatorParameters.Clear();
                model.CreatorMatchesByName = false;
                return new CreatorResolution(supplied, failed: true);
            }

            const string RefusedAtBuild = "the reflection path refuses this mapping with a CborException when it is built";

            if (hasListedKeys && listedCount != creator.Parameters.Length)
            {
                return Refuse($"[CborConstructor] lists {listedCount} member "
                    + (byName ? "names" : "indexes")
                    + $" for {creator.Parameters.Length} constructor parameters; {RefusedAtBuild}");
            }

            bool anyUnlistedLeftToSetter = false;
            bool anyRoutedPastSetter = false;

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

                string? listed = hasListedKeys && byName ? names![i] : null;
                int? listedIndex = hasListedKeys && !byName ? indexes![i] : null;

                WalkedMember? match = null;

                foreach (WalkedMember candidate in walked)
                {
                    bool hit = hasListedKeys
                        ? (byName
                            ? string.Equals(candidate.CborName, listed, StringComparison.OrdinalIgnoreCase)
                            : candidate.Index == listedIndex)
                        : (byName
                            ? string.Equals(candidate.CborName, parameter.Name, StringComparison.OrdinalIgnoreCase)
                            : string.Equals(candidate.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));

                    if (hit)
                    {
                        match = candidate;
                        break;
                    }
                }

                if (match is null && byName && !hasListedKeys)
                {
                    WalkedMember? sameCSharpName = walked
                        .Where(w => string.Equals(w.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
                        .Select(w => (WalkedMember?)w)
                        .FirstOrDefault();

                    if (sameCSharpName is { } renamed)
                    {
                        if (renamed.CanWrite)
                        {
                            // Left at its default, as the reflection path leaves it; the setter
                            // supplies the member. The runtime's own name matching does exactly that.
                            anyUnlistedLeftToSetter = true;
                            model.CreatorParameters.Add((FullNameOf(parameter.Type), string.Empty, null, parameter.Name));
                            continue;
                        }

                        if (renamed.InitOnly)
                        {
                            if (!IsVerbatimPositionalParameter(creator, parameter, renamed.Symbol))
                            {
                                // Passing the value through the constructor is the same object only
                                // if the parameter is assigned verbatim, which is what a record's
                                // primary constructor does and a hand-written one may not: the
                                // reflection path hands it the default and runs the init accessor
                                // afterwards, so a constructor that transforms its argument differs.
                                return Refuse($"parameter '{parameter.Name}' shares its name with '{renamed.Name}', an init-only member serialized as "
                                    + $"'{renamed.CborName}'. The reflection path hands the parameter its default and runs the init accessor afterwards, "
                                    + "which a generated context can only reproduce for a record's primary constructor. Name the parameter after the "
                                    + "serialized key, give the member a setter, or list the members in [CborConstructor]");
                            }

                            if (!_isIdentical(renamed.Type, parameter.Type))
                            {
                                return Refuse(DescribeTypeMismatch(parameter, renamed));
                            }

                            anyRoutedPastSetter = true;
                            supplied.Add(renamed.Name);
                            model.CreatorParameters.Add((FullNameOf(parameter.Type), renamed.CborName, renamed.Index, parameter.Name));
                            continue;
                        }

                        return Refuse($"parameter '{parameter.Name}' shares its name with '{renamed.Name}', which is serialized as "
                            + $"'{renamed.CborName}' and has no setter, so the reflection path leaves the parameter at its default "
                            + "and never sets the member. Name the parameter after the serialized key, give the member a setter, "
                            + "or list the members in [CborConstructor]");
                    }
                }

                if (match is null)
                {
                    return Refuse(hasListedKeys
                        ? $"[CborConstructor] lists member {(byName ? "name" : "index")} '{(byName ? listed : listedIndex?.ToString(CultureInfo.InvariantCulture))}', which matches no serialized member; {RefusedAtBuild}"
                        : $"parameter '{parameter.Name}' matches no serialized member, so the reflection path would leave it at its default value; "
                            + "name it after a member, or list the members in [CborConstructor]");
                }

                if (!_isIdentical(match.Value.Type, parameter.Type))
                {
                    return Refuse(DescribeTypeMismatch(parameter, match.Value));
                }

                supplied.Add(match.Value.Name);

                // Listed keys are passed on verbatim -- CreatorMapping uses them as given to look the
                // value up, after matching the member case-insensitively.
                model.CreatorParameters.Add((
                    FullNameOf(parameter.Type),
                    listed ?? match.Value.CborName,
                    listedIndex ?? match.Value.Index,
                    parameter.Name));
            }

            if (anyUnlistedLeftToSetter && anyRoutedPastSetter)
            {
                return Refuse("some parameters are left to their member's setter and others, for an init-only member, "
                    + "are passed the value; the two cannot be expressed together. Name the parameters after the "
                    + "serialized keys, or list the members in [CborConstructor]");
            }

            if (anyUnlistedLeftToSetter)
            {
                // No names at all: the runtime matches each parameter the way the reflection path does.
                model.CreatorMatchesByName = true;
            }

            return new CreatorResolution(supplied, failed: false);
        }

        /// <summary>
        /// Whether passing this constructor parameter's value through the constructor leaves the same
        /// object as the reflection path (default argument, then the init accessor): the compiler
        /// assigns it, unchanged, to this member -- the record's primary constructor and a property
        /// the compiler synthesised from its parameter list, on that same record -- and nothing else
        /// reads it. A synthesised property is told apart from one the
        /// record declares by its syntax: a parameter of the header, not a property declaration (the
        /// symbol does not report itself as implicitly declared).
        /// </summary>
        /// <remarks>
        /// Both halves are needed. A record may declare the property itself
        /// (<c>public int X { get; init; } = X * 2;</c>), in which case the parameter is not assigned
        /// verbatim, and a derived record's parameter can name a property of its base, which the
        /// derived constructor passes to the base constructor and which is not synthesised again.
        /// The primary constructor is told apart from an explicit one by where it is declared: the
        /// record's own header, not a constructor declaration. And the parameter must not be read
        /// anywhere else -- see <see cref="IsParameterRead"/>.
        /// </remarks>
        private bool IsVerbatimPositionalParameter(IMethodSymbol constructor, IParameterSymbol parameter, ISymbol member)
        {
            return constructor.ContainingType.IsRecord
                && constructor.DeclaringSyntaxReferences.Any(
                    reference => reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax)
                && member.DeclaringSyntaxReferences.Any(
                    reference => reference.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.ParameterSyntax)
                && SymbolEqualityComparer.Default.Equals(member.ContainingType, constructor.ContainingType)
                && !IsParameterRead(constructor.ContainingType, parameter);
        }

        /// <summary>
        /// Whether anything in the record's declarations reads this primary-constructor parameter.
        /// </summary>
        /// <remarks>
        /// The reflection path hands the parameter its default, so any read of it -- a field or
        /// property initializer, a base-constructor argument -- sees zero there and the value on a
        /// generated path that passes it through. In a record the positional name inside an ordinary
        /// member body is the <em>property</em>, not the parameter, so those are not reads of it and
        /// do not count; the walk finds exactly the initializers and base arguments. Every part of a
        /// partial record is searched.
        /// </remarks>
        private bool IsParameterRead(INamedTypeSymbol record, IParameterSymbol parameter)
        {
            foreach (SyntaxReference reference in record.DeclaringSyntaxReferences)
            {
                SyntaxNode declaration = reference.GetSyntax();
                SemanticModel model = _semanticModelFor(declaration.SyntaxTree);

                foreach (Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax name in declaration
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>())
                {
                    if (name.Identifier.ValueText != parameter.Name)
                    {
                        continue;
                    }

                    ISymbol? bound = model.GetSymbolInfo(name).Symbol;

                    if (bound is IParameterSymbol read
                        && read.Name == parameter.Name
                        && SymbolEqualityComparer.Default.Equals(read.ContainingSymbol, parameter.ContainingSymbol))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string DescribeTypeMismatch(IParameterSymbol parameter, WalkedMember member)
        {
            return $"parameter '{parameter.Name}' is a {parameter.Type.ToDisplayString()} but is filled from member "
                + $"'{member.Name}', a {member.Type.ToDisplayString()}; "
                + "the reflection path refuses this mapping with a CborException when it is built";
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

        private bool HasSeveralUnmarkedConstructors(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named)
            {
                return false;
            }

            List<IMethodSymbol> constructors = named.InstanceConstructors
                .Where(c => !c.IsStatic && !c.IsImplicitlyDeclared)
                .ToList();

            return constructors.Count > 1
                && !constructors.Any(c => c.Parameters.Length == 0 || HasAttribute(c, "CborConstructorAttribute"));
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

            // [CborConstructor] targets a constructor, not the type. Reflection maps it as the creator of
            // a concrete type, from that type's own constructors; a generated factory is `new T()` plus
            // the setters, so it would build the value without the constructor ever running.
            if (type is INamedTypeSymbol { IsAbstract: false } namedType)
            {
                foreach (IMethodSymbol constructor in namedType.InstanceConstructors)
                {
                    if (HasAttribute(constructor, "CborConstructorAttribute"))
                    {
                        Report(constructor.Locations.FirstOrDefault(), type.ToDisplayString(), "[CborConstructor]");
                    }
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
        /// A member hiding a base member with <c>new</c> is deliberately not collapsed.
        /// <c>GetProperties</c> returns both of those, so the reflection path sees two members under one
        /// name and refuses the mapping; the generator reports <c>CBOR1013</c> for the same type, which
        /// is the same answer given earlier.
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

            for (ITypeSymbol? current = type;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
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

            if (HasInheritedAttribute(member, "CborIgnoreAttribute"))
            {
                return false;
            }

            if (HasInheritedAttribute(member, "CborPropertyAttribute"))
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
        /// The constructor a read builds this type through, or null where <c>new T()</c> will do.
        /// </summary>
        /// <remarks>
        /// Mirrors <c>DefaultObjectMappingConvention</c>: a <c>[CborConstructor]</c> wins, and otherwise
        /// a type with no parameterless constructor is built through the one it has — which is what a
        /// positional record is, and why records could not round-trip through a generated context.
        /// <para>
        /// Deliberately narrower than the reflection path in one case. Where several constructors are
        /// declared, none marked and none parameterless, that path takes <c>constructorInfos[0]</c>;
        /// nothing guarantees Roslyn orders them as reflection does, so picking one here could silently
        /// build a type through a different constructor than the reflection path uses. For a class that
        /// case keeps CBOR1010, whose remedy — add a parameterless constructor, or mark one with
        /// <c>[CborConstructor]</c> — resolves the ambiguity for both paths at once. A struct always
        /// has <c>new T()</c>, so it is reported the same way only where reading it that way would lose
        /// a member (one with no setter); otherwise default-plus-setters is a complete read.
        /// </para>
        /// </remarks>
        private IMethodSymbol? FindCreatorConstructor(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol named || named.IsAbstract)
            {
                return null;
            }

            // Every accessibility is considered, as the reflection path does (it binds NonPublic), so
            // that a creator the context cannot call is reported by the caller rather than silently
            // passed over for `new T()`. Implicitly declared ones are excluded, which is what keeps a
            // record workable: its copy constructor is compiler-generated and protected, while the
            // primary constructor is neither, so a positional record has exactly one candidate rather
            // than two. Measured, not assumed -- Roslyn reports the primary as
            // IsImplicitlyDeclared: false.
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

            // A record may declare its copy constructor itself (`private R(R original)`, the way a
            // sealed record keeps it from being called). It is not a candidate: the reflection path
            // takes the first constructor in declaration order, which for a record is the primary one
            // (measured), and the copy constructor never is. Only for a record -- in a class a
            // `C(C other)` is an ordinary constructor, and which of two the reflection path takes is
            // not something to guess at.
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
