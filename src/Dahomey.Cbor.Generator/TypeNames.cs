using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dahomey.Cbor.Generator
{
    /// <summary>Identifier construction shared by the registration emitter and the CDDL emitter.</summary>
    internal static class TypeNames
    {
        /// <summary>
        /// The stem of a generated file's hint name: the context's fully qualified name, with every
        /// character that is not a letter, digit or underscore folded to '.'. Keying on the simple name
        /// alone makes two same-named contexts in different namespaces collide, and the generator
        /// throws. Shared so a context's two emitted files always agree on the stem and differ only in
        /// the suffix each appends.
        /// </summary>
        public static string HintNameStem(INamedTypeSymbol contextSymbol)
        {
            StringBuilder builder = new StringBuilder();

            foreach (char character in contextSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            {
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '.');
            }

            return builder.ToString().Trim('.');
        }

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
        /// Maps each type to its CDDL rule name, in three steps that each guarantee something the next
        /// one relies on. A short name (<see cref="AccessorName"/>) that no other type in
        /// <paramref name="ordered"/> shares is kept exactly as produced; a short name shared by two or
        /// more types -- two same-named nested types in different namespaces, say -- is re-derived for
        /// every member of that collision from the full type key via
        /// <see cref="QualifiedAccessorName"/>, so the result never depends on which was seen first.
        /// Whatever those produce is then folded onto RFC 8610's identifier syntax
        /// (<see cref="SanitizeRuleName"/>), and finally made unique
        /// (<see cref="ResolveRemainingCollisions"/>).
        /// </summary>
        /// <remarks>
        /// The last step is what makes the guarantee unconditional, and it is load-bearing rather than
        /// defensive: escaping folds a character outside ASCII onto a <c>_</c> sequence that a C# name
        /// is free to contain literally, so <c>Café</c> and <c>Caf_00E9</c> reach it as one name. A
        /// duplicate rule name is the one malformation an emitted schema can carry without any tool
        /// complaining -- the gem reads a file whose second definition of a rule silently shadows the
        /// first -- so it cannot be left to the shape of the names.
        /// </remarks>
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

            Dictionary<string, string> candidates = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, List<TypeModel>> entry in byShortName)
            {
                foreach (TypeModel model in entry.Value)
                {
                    string key = model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    candidates[key] = SanitizeRuleName(
                        entry.Value.Count == 1 ? entry.Key : QualifiedAccessorName(model.Symbol));
                }
            }

            return ResolveRemainingCollisions(candidates);
        }

        /// <summary>
        /// Gives every type a rule name no other type in the same schema holds, keeping each candidate
        /// where it is already unique and appending <c>-2</c>, <c>-3</c> and so on to the rest.
        /// </summary>
        /// <remarks>
        /// Which member of a collision keeps the bare name is decided by ordinal order of the full type
        /// key, and the groups are walked in ordinal order of the candidate, so the whole mapping is a
        /// function of the set of types alone. That is the property a rule name needs and an accessor
        /// name does not: a schema is a published artifact, compared against other copies of itself, so
        /// a name that depended on the order <c>ordered</c> happened to arrive in would make two builds
        /// of one context disagree. A suffixed name is checked against the names already handed out
        /// rather than assumed free, since a candidate elsewhere in the schema may already read
        /// <c>X-2</c>.
        /// </remarks>
        private static IReadOnlyDictionary<string, string> ResolveRemainingCollisions(
            IReadOnlyDictionary<string, string> candidates)
        {
            Dictionary<string, List<string>> keysByCandidate =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> candidate in candidates)
            {
                if (!keysByCandidate.TryGetValue(candidate.Value, out List<string>? keys))
                {
                    keys = new List<string>();
                    keysByCandidate[candidate.Value] = keys;
                }

                keys.Add(candidate.Key);
            }

            Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

            // Uncontested candidates first, and all of them, so a suffix minted below cannot take a
            // name that some other type was always going to hold.
            foreach (KeyValuePair<string, List<string>> entry in keysByCandidate)
            {
                if (entry.Value.Count == 1)
                {
                    names[entry.Value[0]] = entry.Key;
                    used.Add(entry.Key);
                }
            }

            IEnumerable<string> contested = keysByCandidate
                .Where(entry => entry.Value.Count > 1)
                .Select(entry => entry.Key)
                .OrderBy(candidate => candidate, StringComparer.Ordinal);

            foreach (string candidate in contested)
            {
                List<string> keys = keysByCandidate[candidate];
                keys.Sort(StringComparer.Ordinal);

                foreach (string key in keys)
                {
                    string name = candidate;

                    for (int suffix = 2; !used.Add(name); suffix++)
                    {
                        name = candidate + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                    }

                    names[key] = name;
                }
            }

            return names;
        }

        /// <summary>
        /// Folds an identifier onto RFC 8610's <c>id</c> production, which is ASCII-only:
        /// <c>id = EALPHA *(*("-" / ".") (EALPHA / DIGIT))</c>, where <c>EALPHA</c> is an ASCII letter,
        /// <c>@</c>, <c>_</c> or <c>$</c>. A C# type name may contain any Unicode letter or digit, and
        /// a rule named with one is not a schema a tool merely dislikes: <c>cddl</c> stops on it with a
        /// parse error and exits 65, and a schema that does not parse cannot fail an instance check, so
        /// nothing downstream reports it either.
        /// </summary>
        /// <remarks>
        /// Each character outside the production becomes <c>_</c> followed by its code point in
        /// uppercase hex, which keeps two names differing only outside ASCII apart -- dropping the
        /// character instead would collapse <c>Café</c> and <c>Cafe</c> onto one rule. A character
        /// outside the BMP is one code point arriving as a surrogate pair, so the pair is escaped
        /// together rather than as two lone surrogates.
        /// </remarks>
        private static string SanitizeRuleName(string name)
        {
            StringBuilder builder = new StringBuilder(name.Length);

            for (int index = 0; index < name.Length; index++)
            {
                char character = name[index];

                if (IsIdCharacter(character))
                {
                    builder.Append(character);
                    continue;
                }

                int codePoint = character;

                if (char.IsHighSurrogate(character)
                    && index + 1 < name.Length
                    && char.IsLowSurrogate(name[index + 1]))
                {
                    codePoint = char.ConvertToUtf32(character, name[index + 1]);
                    index++;
                }

                builder.Append('_').Append(codePoint.ToString("X4", CultureInfo.InvariantCulture));
            }

            // `id` has to open with EALPHA. Nothing a C# type name can produce arrives here -- an
            // identifier cannot begin with a digit, and an escape begins with '_' -- so this keeps the
            // method total rather than covering a case that has a name.
            if (builder.Length == 0 || !IsIdStartCharacter(builder[0]))
            {
                builder.Insert(0, '_');
            }

            return builder.ToString();
        }

        /// <summary><c>EALPHA</c>: an ASCII letter, or one of <c>@</c>, <c>_</c>, <c>$</c>.</summary>
        private static bool IsIdStartCharacter(char character)
        {
            return (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || character == '@'
                || character == '_'
                || character == '$';
        }

        /// <summary>
        /// <c>EALPHA / DIGIT</c>, plus the <c>-</c> and <c>.</c> the production allows between them.
        /// Neither separator can arrive from a C# name; <c>-</c> is what the qualified forms above join
        /// segments with, and it is accepted here so sanitizing one of those is a no-op.
        /// </summary>
        private static bool IsIdCharacter(char character)
        {
            return IsIdStartCharacter(character)
                || (character >= '0' && character <= '9')
                || character == '-'
                || character == '.';
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
