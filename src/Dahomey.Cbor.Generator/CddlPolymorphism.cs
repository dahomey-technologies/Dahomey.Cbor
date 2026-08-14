using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Which CDDL rules a type contributes, and what a use site referring to it must name.
    /// </summary>
    /// <remarks>
    /// Two shapes exist because <c>DiscriminatorMemberConverter.ShouldSerialize</c> decides on the
    /// static type at the call site: under the effective default policy of <c>Auto</c> the
    /// discriminator is written only when <c>obj.GetType() != declaredType</c>, so
    /// <c>Cbor.Serialize&lt;Shape&gt;(circle)</c> writes it and <c>Cbor.Serialize&lt;Circle&gt;(circle)</c>
    /// does not.
    /// </remarks>
    internal sealed class PolymorphicShape
    {
        /// <summary>
        /// Types that attach to this one as arms of its type choice. Nearest rather than transitive, so
        /// a three-level hierarchy nests (<c>A-poly = B-poly</c>, <c>B-poly = ... / C-poly</c>) instead
        /// of repeating C in both choices; walking up past uncollected intermediates keeps a base
        /// declared without its intermediate reachable.
        /// </summary>
        public List<TypeModel> Subtypes { get; } = new List<TypeModel>();

        /// <summary>False for abstract classes and interfaces, which are never written as themselves.</summary>
        public bool IsInstantiable { get; set; }

        /// <summary>
        /// Whether this type is itself reachable through a base within this schema, which is what
        /// makes its own discriminated shape writable.
        /// </summary>
        public bool HasCollectedBase { get; set; }

        /// <summary>
        /// Whether <c>ObjectMapping.SetDiscriminator</c> would actually insert the discriminator
        /// mapping: it does so only for concrete, non-struct classes, so a struct carrying a
        /// discriminator attribute writes none.
        /// </summary>
        public bool WritesDiscriminator { get; set; }

        /// <summary>
        /// Whether anything below this type in the hierarchy writes a discriminator. A type choice
        /// whose arms carry none describes a document no consumer can tell apart, which is what
        /// CBOR1012 reports.
        /// </summary>
        public bool HasDiscriminatedDescendant { get; set; }

        /// <summary>The effective policy, already resolved to Never / Always / Auto.</summary>
        public string Policy { get; set; } = "Auto";

        /// <summary>The undiscriminated shape, written when the static type is exactly this type.</summary>
        public bool EmitsBare { get; set; }

        /// <summary>The through-a-base shape: a discriminated rule, or a type choice over subtypes.</summary>
        public bool EmitsPoly { get; set; }

        /// <summary>
        /// What a member declared as this type must name. A member typed as a base may hold any
        /// subtype, so it names the choice; a member typed as a leaf names that leaf's only rule.
        /// </summary>
        public string ReferenceName(string ruleName)
        {
            return EmitsPoly && (Subtypes.Count > 0 || !EmitsBare) ? ruleName + "-poly" : ruleName;
        }

        /// <summary>What an enclosing type choice names for this subtype.</summary>
        public string ArmName(string ruleName)
        {
            return EmitsPoly ? ruleName + "-poly" : ruleName;
        }

        /// <summary>
        /// The second rule name this type occupies, or null when it is emitted under one name only.
        /// </summary>
        /// <remarks>
        /// <see cref="ArmName"/> and <see cref="ReferenceName"/> mint this from a rule name that has
        /// already been handed out, so it is never a candidate the uniqueness pass sees -- which is why
        /// <see cref="TypeNames.BuildRuleNames"/> asks for it and reserves the two names together. A
        /// type whose own declaration name reads <c>X-poly</c> would otherwise be free to take the
        /// derived form of a polymorphic <c>X</c>, and two rules would be emitted under one name.
        /// <para>
        /// An over-approximation by one case: a type reported by CBOR1012 is skipped before either rule
        /// is emitted, so its reservation holds a name nothing uses. That costs another type a suffix in
        /// a compilation that already carries an error, which is cheaper than making the reservation
        /// depend on diagnostics that have not run yet.
        /// </para>
        /// </remarks>
        public string? DerivedRuleName(string ruleName)
        {
            return EmitsPoly ? ArmName(ruleName) : null;
        }
    }

    /// <summary>
    /// Resolves the inheritance relationships between the object types a context collected, which is
    /// what turns a flat type list into the type choices <see cref="CddlEmitter"/> emits.
    /// </summary>
    internal static class CddlPolymorphism
    {
        /// <remarks>
        /// Takes no rule names, and that is what makes the ordering work: <see cref="TypeNames.BuildRuleNames"/>
        /// needs to know which types emit a <c>-poly</c> rule *before* it hands names out, so the one
        /// thing here that does depend on a name -- the order of a choice's arms -- is settled afterwards
        /// by <see cref="SortArms"/>. Nothing else in a shape is derived from a rule name.
        /// </remarks>
        public static Dictionary<string, PolymorphicShape> BuildShapes(
            IReadOnlyList<TypeModel> ordered,
            IReadOnlyDictionary<string, TypeModel> byKey)
        {
            Dictionary<string, PolymorphicShape> shapes = new Dictionary<string, PolymorphicShape>();

            foreach (TypeModel model in ordered)
            {
                if (model.Kind != TypeKind.Object)
                {
                    continue;
                }

                bool isInterface = model.Symbol.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface;

                shapes[Key(model.Symbol)] = new PolymorphicShape
                {
                    IsInstantiable = !model.Symbol.IsAbstract && !isInterface,
                    WritesDiscriminator = model.Discriminator is not null
                        && !model.Symbol.IsAbstract && !isInterface && !model.Symbol.IsValueType,
                    Policy = model.DiscriminatorPolicy ?? "Auto",
                };
            }

            foreach (TypeModel model in ordered)
            {
                if (model.Kind != TypeKind.Object)
                {
                    continue;
                }

                foreach (string parentKey in PolymorphicBases(model.Symbol, byKey))
                {
                    shapes[parentKey].Subtypes.Add(model);
                    shapes[Key(model.Symbol)].HasCollectedBase = true;
                }
            }

            foreach (PolymorphicShape shape in shapes.Values)
            {
                shape.EmitsBare = shape.IsInstantiable
                    && !(shape.WritesDiscriminator && shape.Policy == "Always");
                shape.EmitsPoly = shape.Subtypes.Count > 0
                    || (shape.WritesDiscriminator && shape.Policy != "Never");
            }

            foreach (PolymorphicShape shape in shapes.Values)
            {
                shape.HasDiscriminatedDescendant = AnyDiscriminatorBeneath(shape, shapes);
            }

            return shapes;
        }

        /// <summary>
        /// Orders each type choice's arms by rule name rather than by collection order, so the emitted
        /// choice is a pure function of the source and the schema stays byte-stable across builds.
        /// </summary>
        public static void SortArms(
            IReadOnlyDictionary<string, PolymorphicShape> shapes,
            IReadOnlyDictionary<string, string> ruleNames)
        {
            foreach (PolymorphicShape shape in shapes.Values)
            {
                shape.Subtypes.Sort((left, right) => string.CompareOrdinal(
                    ruleNames[Key(left.Symbol)], ruleNames[Key(right.Symbol)]));
            }
        }

        /// <summary>
        /// The types this one attaches to as an arm of their type choice.
        /// </summary>
        /// <remarks>
        /// The base-class chain is searched first and wins outright, so a class hierarchy always
        /// attaches to its nearest collected base class rather than to an interface that happens to be
        /// collected as well. Interfaces are then considered, and two kinds are dropped: one the nearest
        /// collected base class already implements (the base is the nearer arm, and listing this type as
        /// well would repeat it inside its own base's choice), and one that another collected interface
        /// of this type already refines (so a chain <c>I2 : I1</c> nests as <c>I1-poly = I2-poly</c>).
        /// <para>
        /// What survives is every <em>most-derived</em> collected interface, and a type implementing two
        /// unrelated ones attaches to both -- it really is reachable through either, so omitting it from
        /// one of the two choices would describe a narrower contract than the serializer writes. That is
        /// the tie-break: neither interface is preferred, both get the arm. Each choice is still sorted
        /// by rule name afterwards, so the output stays byte-stable.
        /// </para>
        /// </remarks>
        private static IEnumerable<string> PolymorphicBases(
            ITypeSymbol type, IReadOnlyDictionary<string, TypeModel> byKey)
        {
            INamedTypeSymbol? baseClass = NearestCollectedBaseClass(type, byKey);

            if (baseClass is not null)
            {
                yield return Key(baseClass);
            }

            foreach (INamedTypeSymbol candidate in type.AllInterfaces)
            {
                if (!IsCollectedObject(candidate, byKey))
                {
                    continue;
                }

                if (baseClass is not null && Implements(baseClass, candidate))
                {
                    continue;
                }

                bool refinedByAnother = type.AllInterfaces.Any(other =>
                    !SymbolEqualityComparer.Default.Equals(other, candidate)
                    && IsCollectedObject(other, byKey)
                    && Implements(other, candidate));

                if (!refinedByAnother)
                {
                    yield return Key(candidate);
                }
            }
        }

        private static INamedTypeSymbol? NearestCollectedBaseClass(
            ITypeSymbol type, IReadOnlyDictionary<string, TypeModel> byKey)
        {
            for (INamedTypeSymbol? current = (type as INamedTypeSymbol)?.BaseType;
                 current is not null;
                 current = current.BaseType)
            {
                if (IsCollectedObject(current, byKey))
                {
                    return current;
                }
            }

            return null;
        }

        private static bool IsCollectedObject(
            ITypeSymbol type, IReadOnlyDictionary<string, TypeModel> byKey)
        {
            return byKey.TryGetValue(Key(type), out TypeModel? model) && model.Kind == TypeKind.Object;
        }

        private static bool Implements(ITypeSymbol type, INamedTypeSymbol candidate)
        {
            return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, candidate));
        }

        /// <summary>
        /// Terminates because the parent relation is acyclic: a base-class chain never revisits a type,
        /// and an interface can never refine one that refines it.
        /// </summary>
        private static bool AnyDiscriminatorBeneath(
            PolymorphicShape shape, IReadOnlyDictionary<string, PolymorphicShape> shapes)
        {
            foreach (TypeModel subtype in shape.Subtypes)
            {
                PolymorphicShape subtypeShape = shapes[Key(subtype.Symbol)];

                if (subtypeShape.WritesDiscriminator || AnyDiscriminatorBeneath(subtypeShape, shapes))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Key(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }
}
