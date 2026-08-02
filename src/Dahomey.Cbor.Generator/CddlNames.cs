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
        /// Maps each type to its CDDL rule name. Two distinct types sharing a simple name are both
        /// qualified with their flattened namespace, so the result never depends on which was seen
        /// first.
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
                    names[key] = entry.Value.Count == 1 ? entry.Key : Qualify(model.Symbol, entry.Key);
                }
            }

            return names;
        }

        /// <summary>
        /// <c>My-Models-Person</c>. RFC 8610 ids accept '-' between alphanumerics, so a flattened
        /// namespace is a legal rule name.
        /// </summary>
        private static string Qualify(ITypeSymbol type, string shortName)
        {
            if (type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace)
            {
                return shortName;
            }

            string prefix = type.ContainingNamespace.ToDisplayString().Replace(".", "-");
            return prefix + "-" + shortName;
        }
    }
}
