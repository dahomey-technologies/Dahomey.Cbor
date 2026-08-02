using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Generator
{
    /// <summary>Identifier construction shared by the registration emitter and the CDDL emitter.</summary>
    internal static class CddlNames
    {
        /// <summary>
        /// Property name for a type's accessor: <c>Person</c>, and <c>ListOfPerson</c> for
        /// <c>List&lt;Person&gt;</c>, so closed generics get a legal, predictable identifier. Also the
        /// basis of CDDL rule names, which accept the same character set.
        /// </summary>
        public static string AccessorName(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return "ArrayOf" + AccessorName(array.ElementType);
            }

            if (type is INamedTypeSymbol { IsGenericType: true } named)
            {
                string baseName = named.Name;
                string arguments = string.Concat(named.TypeArguments.Select(AccessorName));
                return $"{baseName}Of{arguments}";
            }

            return type.Name;
        }

        /// <summary>
        /// Maps each type to its CDDL rule name. A short name (<see cref="AccessorName"/>) that no
        /// other type in <paramref name="ordered"/> shares is kept exactly as produced. A short name
        /// shared by two or more types -- two different closed generics whose type arguments only
        /// differ by namespace (<c>Envelope&lt;Left.Item&gt;</c> vs. <c>Envelope&lt;Right.Item&gt;</c>),
        /// or two same-named nested types in different namespaces -- is re-derived for every member of
        /// that collision from the full type key via <see cref="QualifiedAccessorName"/>, so the result
        /// never depends on which was seen first and never collides again.
        /// </summary>
        public static IReadOnlyDictionary<string, string> BuildRuleNames(IReadOnlyList<TypeModel> ordered)
        {
            Dictionary<string, List<TypeModel>> byShortName = new Dictionary<string, List<TypeModel>>();

            foreach (TypeModel model in ordered)
            {
                string shortName = AccessorName(model.Symbol);

                if (!byShortName.TryGetValue(shortName, out List<TypeModel>? bucket))
                {
                    bucket = new List<TypeModel>();
                    byShortName[shortName] = bucket;
                }

                bucket.Add(model);
            }

            Dictionary<string, string> names = new Dictionary<string, string>();

            foreach (KeyValuePair<string, List<TypeModel>> entry in byShortName)
            {
                foreach (TypeModel model in entry.Value)
                {
                    string key = model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    names[key] = entry.Value.Count == 1 ? entry.Key : QualifiedAccessorName(model.Symbol);
                }
            }

            return names;
        }

        /// <summary>
        /// Same shape as <see cref="AccessorName"/>, but every named type in the tree -- the type
        /// itself and, recursively, each generic type argument -- is rendered by
        /// <see cref="QualifiedSimpleName"/> instead of a bare <c>type.Name</c>. Only called once a
        /// short name is known to collide, so <c>Envelope&lt;Left.Item&gt;</c> and
        /// <c>Envelope&lt;Right.Item&gt;</c> (both <c>EnvelopeOfItem</c> under
        /// <see cref="AccessorName"/>, because that method drops namespaces entirely) resolve to
        /// <c>Envelope-Of-Left-Item</c> and <c>Envelope-Of-Right-Item</c>.
        /// </summary>
        private static string QualifiedAccessorName(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return "ArrayOf-" + QualifiedAccessorName(array.ElementType);
            }

            if (type is INamedTypeSymbol { IsGenericType: true } named)
            {
                string baseName = QualifiedSimpleName(named);
                string arguments = string.Join("-", named.TypeArguments.Select(QualifiedAccessorName));
                return $"{baseName}-Of-{arguments}";
            }

            return QualifiedSimpleName(type);
        }

        /// <summary>
        /// <c>N-Outer-Inner</c>: the type's namespace (dots flattened to '-', omitted when global),
        /// then its containing types outermost-first, then its own name. RFC 8610 ids accept '-'
        /// between alphanumerics, so the flattened chain is a legal rule name fragment. Unlike
        /// <see cref="AccessorName"/>, this reaches the containing-type chain too, so nested
        /// <c>N.Outer.Inner</c> and <c>N.Other.Inner</c> -- indistinguishable by <c>type.Name</c> alone
        /// -- come out as <c>N-Outer-Inner</c> and <c>N-Other-Inner</c>.
        /// </summary>
        private static string QualifiedSimpleName(ITypeSymbol type)
        {
            List<string> segments = new List<string>();

            if (type.ContainingNamespace is not null && !type.ContainingNamespace.IsGlobalNamespace)
            {
                segments.Add(type.ContainingNamespace.ToDisplayString().Replace(".", "-"));
            }

            List<string> containingTypes = new List<string>();

            for (INamedTypeSymbol? outer = (type as INamedTypeSymbol)?.ContainingType;
                outer is not null;
                outer = outer.ContainingType)
            {
                containingTypes.Insert(0, outer.Name);
            }

            segments.AddRange(containingTypes);
            segments.Add(type.Name);

            return string.Join("-", segments);
        }
    }
}
