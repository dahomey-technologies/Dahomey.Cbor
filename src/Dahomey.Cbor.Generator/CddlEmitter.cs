using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Renders an RFC 8610 CDDL schema for a context, as a second emit target over the same
    /// <see cref="TypeModel"/> list the registration emitter consumes.
    /// </summary>
    /// <remarks>
    /// This is a separate walk by necessity: <see cref="Emitter"/> registers one converter per type
    /// keyed on <c>typeof(T)</c> and resolves members through <c>DelegateMemberMapping</c> at run time,
    /// so there is no per-member emit site a schema could hang off.
    /// </remarks>
    internal static class CddlEmitter
    {
        public static string EmitSchema(
            IReadOnlyList<TypeModel> ordered,
            IReadOnlyList<ITypeSymbol> roots,
            GenerationOptions options,
            List<DiagnosticInfo> diagnostics)
        {
            Dictionary<string, TypeModel> byKey = new Dictionary<string, TypeModel>();

            foreach (TypeModel model in ordered)
            {
                byKey[model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)] = model;
            }

            // Shapes first, because a polymorphic type is emitted under a second rule name derived from
            // its first, and the uniqueness pass has to reserve both together. Only the order of a
            // choice's arms depends on the names, so that one step waits.
            Dictionary<string, PolymorphicShape> shapes = CddlPolymorphism.BuildShapes(ordered, byKey);
            IReadOnlyDictionary<string, string> ruleNames = TypeNames.BuildRuleNames(ordered, shapes);
            CddlPolymorphism.SortArms(shapes, ruleNames);

            // Use sites resolve through their own name table: a member declared as a base type names
            // that base's `-poly` rule, a member declared as a leaf names the leaf's bare rule. Keeping
            // it separate from `ruleNames` -- which stays the declaration name -- is what lets
            // CddlTypeReference stay unaware of polymorphism entirely.
            Dictionary<string, string> referenceNames = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> entry in ruleNames)
            {
                referenceNames[entry.Key] = shapes.TryGetValue(entry.Key, out PolymorphicShape? shape)
                    ? shape.ReferenceName(entry.Value)
                    : entry.Value;
            }

            HashSet<string> rootKeys = new HashSet<string>();

            foreach (ITypeSymbol root in roots)
            {
                rootKeys.Add(root.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            StringBuilder builder = new StringBuilder();

            AppendHeader(builder, ordered, shapes);

            foreach (TypeModel model in InEmissionOrder(ordered, roots))
            {
                if (model.Kind == TypeKind.Enum)
                {
                    EmitEnumRule(builder, model, ruleNames, options);
                    continue;
                }

                if (model.Kind != TypeKind.Object)
                {
                    string key = model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                    // Unsupported roots are excluded: TypeCollector has already reported CBOR1002 for
                    // them, and a second diagnostic about the same declaration adds no information.
                    if (model.Kind != TypeKind.Unsupported && rootKeys.Contains(key))
                    {
                        EmitRootAliasRule(builder, model, key, byKey, ruleNames, referenceNames, options, diagnostics);
                    }

                    continue;
                }

                EmitObjectRules(builder, model, byKey, ruleNames, referenceNames, shapes, options, diagnostics);
            }

            return builder.ToString();
        }

        /// <summary>
        /// A rule for a declared root that is not an object or an enum -- a collection, an array, a
        /// dictionary, a byte array or a scalar. <c>[CborSerializable(typeof(List&lt;Person&gt;))]</c> is
        /// a supported declaration that <see cref="Emitter"/> emits an accessor for, so the schema owes
        /// it a rule: without one the user gets <c>Person</c> and nothing at all describing the document
        /// they actually write.
        /// </summary>
        /// <remarks>
        /// Only roots. Every other collection in the model list is reached through a member and is
        /// inlined there, so emitting a rule for it as well would add an unreferenced rule per member
        /// and say nothing the member does not already say.
        /// </remarks>
        private static void EmitRootAliasRule(
            StringBuilder builder,
            TypeModel model,
            string key,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> ruleNames,
            IReadOnlyDictionary<string, string> referenceNames,
            GenerationOptions options,
            List<DiagnosticInfo> diagnostics)
        {
            if (!ruleNames.TryGetValue(key, out string? ruleName))
            {
                return;
            }

            string? rendered = CddlTypeReference.RenderRoot(model.Symbol, byKey, referenceNames, options);

            if (rendered is null)
            {
                // Same contract as a member with no representation: an omitted rule is exactly the
                // silent hole CBOR1011 exists to prevent. The root stands in for the member slot, since
                // there is no member to name.
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.NoCddlRepresentation,
                    model.Symbol.Locations.FirstOrDefault(),
                    model.Symbol.ToDisplayString(),
                    "the declared root rule '" + ruleName + "'"));
                return;
            }

            builder.Append(ruleName);
            builder.Append(" = ");
            builder.Append(rendered);
            builder.Append("\n\n");
        }

        private static void AppendHeader(
            StringBuilder builder,
            IReadOnlyList<TypeModel> ordered,
            IReadOnlyDictionary<string, PolymorphicShape> shapes)
        {
            builder.Append("; Generated by Dahomey.Cbor. Do not edit.\n");
            builder.Append("; Describes what the serializer WRITES, closed over the declared members, exact except\n");
            builder.Append("; where a converter's own output is not: `any` for object, the open uint/int form for a\n");
            builder.Append("; [Flags] enum, any length for [* X] and {* K => V}, and a member declared as a polymorphic\n");
            builder.Append("; base admitting every subtype the context declares. One case is narrower than the writer:\n");
            builder.Append("; a uint-backed [Flags] value above int.MaxValue is written as a negative integer, which\n");
            builder.Append("; `uint` rejects.\n");
            builder.Append("; Member types follow their nullable annotations. A member declared non-nullable but left\n");
            builder.Append("; null at run time is written as F6 and will NOT validate against this schema.\n");

            bool anyPolymorphic = false;
            bool anyStringKeyDiscriminator = false;

            foreach (TypeModel model in ordered)
            {
                string key = model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (!shapes.TryGetValue(key, out PolymorphicShape? shape) || !shape.EmitsPoly)
                {
                    continue;
                }

                anyPolymorphic = true;

                if (shape.WritesDiscriminator && model.ObjectFormat == "StringKeyMap")
                {
                    anyStringKeyDiscriminator = true;
                }
            }

            if (anyPolymorphic)
            {
                builder.Append("; A rule named X-poly is what is written when a value is serialized through a base type,\n");
                builder.Append("; which is when the discriminator is present; the bare rule X is what is written when the\n");
                builder.Append("; static type at the call site is exactly X.\n");
            }

            // Only the StringKeyMap placement is invisible in the emitted rules: the IntKeyMap and Array
            // placements show up literally as `0: <value>` and `#6.<tag>(<value>)`, whereas the member
            // name behind a StringKeyMap discriminator comes from a convention the generator cannot see.
            if (anyStringKeyDiscriminator)
            {
                builder.Append("; Assumes the discriminator key is \"_t\"; a custom discriminator convention is\n");
                builder.Append("; registered at run time and is not visible to the generator.\n");
            }

            builder.Append("\n");
        }

        /// <summary>
        /// Declared roots first, in the order written, then everything else in dependency order. The
        /// schema is a reviewable artifact, so a reordering diff is noise that hides real changes.
        /// </summary>
        private static IEnumerable<TypeModel> InEmissionOrder(
            IReadOnlyList<TypeModel> ordered, IReadOnlyList<ITypeSymbol> roots)
        {
            HashSet<string> emitted = new HashSet<string>();

            foreach (ITypeSymbol root in roots)
            {
                string key = root.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                foreach (TypeModel model in ordered)
                {
                    if (model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == key
                        && emitted.Add(key))
                    {
                        yield return model;
                    }
                }
            }

            foreach (TypeModel model in ordered)
            {
                if (emitted.Add(model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                {
                    yield return model;
                }
            }
        }

        private static void EmitObjectRules(
            StringBuilder builder,
            TypeModel model,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> ruleNames,
            IReadOnlyDictionary<string, string> referenceNames,
            IReadOnlyDictionary<string, PolymorphicShape> shapes,
            GenerationOptions options,
            List<DiagnosticInfo> diagnostics)
        {
            string key = model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string ruleName = ruleNames[key];
            PolymorphicShape shape = shapes[key];

            // A type that cannot be instantiated is described only by its subtypes, so it needs a
            // discriminator somewhere beneath it -- a choice whose arms carry none describes a document
            // no consumer can tell apart. Deliberately narrow to the non-instantiable case: an ordinary
            // concrete class with no subtypes is the overwhelmingly common shape and must stay silent.
            if (!shape.IsInstantiable && !shape.HasDiscriminatedDescendant)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.IncompletePolymorphicSchema,
                    model.Symbol.Locations.FirstOrDefault(),
                    model.Symbol.ToDisplayString()));
                return;
            }

            if (shape.EmitsBare)
            {
                builder.Append(ruleName);
                builder.Append(" = ");
                AppendObjectShape(builder, model, byKey, referenceNames, options, diagnostics,
                    withDiscriminator: false, reportDiagnostics: true);
                builder.Append("\n\n");
            }

            if (!shape.EmitsPoly)
            {
                return;
            }

            builder.Append(ruleName);
            builder.Append("-poly = ");

            if (shape.Subtypes.Count == 0)
            {
                AppendObjectShape(builder, model, byKey, referenceNames, options, diagnostics,
                    withDiscriminator: true, reportDiagnostics: !shape.EmitsBare);
                builder.Append("\n\n");
                return;
            }

            List<string> arms = new List<string>();

            // A concrete base is written as itself with no discriminator -- DiscriminatorMemberConverter
            // compares against the declared type, so a member declared as this very type suppresses it
            // -- which is why the bare rule is an arm of this type's own choice.
            if (shape.EmitsBare)
            {
                arms.Add(ruleName);
            }

            // ...and the same type reached through a base of its own does write one. That shape has no
            // rule name available -- `-poly` is taken by this choice -- so it goes in as an anonymous
            // arm. Both arms are needed because one `-poly` rule serves both use sites.
            if (shape.IsInstantiable
                && shape.WritesDiscriminator
                && shape.Policy != "Never"
                && (shape.Policy == "Always" || shape.HasCollectedBase))
            {
                StringBuilder self = new StringBuilder();
                AppendObjectShape(self, model, byKey, referenceNames, options, diagnostics,
                    withDiscriminator: true, reportDiagnostics: !shape.EmitsBare);
                arms.Add(self.ToString());
            }

            foreach (TypeModel subtype in shape.Subtypes)
            {
                string subtypeKey = subtype.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                arms.Add(shapes[subtypeKey].ArmName(ruleNames[subtypeKey]));
            }

            builder.Append(string.Join(" / ", arms));
            builder.Append("\n\n");
        }

        /// <summary>
        /// Appends the map or array body for a type, without a rule name, so the same text can be a
        /// rule of its own or an anonymous arm of a type choice.
        /// </summary>
        private static void AppendObjectShape(
            StringBuilder builder,
            TypeModel model,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> referenceNames,
            GenerationOptions options,
            List<DiagnosticInfo> diagnostics,
            bool withDiscriminator,
            bool reportDiagnostics)
        {
            bool isArray = model.ObjectFormat == "Array";

            builder.Append(isArray ? "[\n" : "{\n");

            // SetDiscriminator inserts this at index 0 of the member mappings at registration, after
            // SetMemberMappings has replaced that list, so it never appears in TypeModel.Members. It is
            // also inserted only for concrete non-struct classes, which is why an abstract base emits a
            // type choice instead of a rule of its own.
            if (withDiscriminator)
            {
                builder.Append("  ");

                switch (model.ObjectFormat)
                {
                    case "Array":
                        // The tag wraps the first element only, and the array length counts it.
                        builder.Append("#6.");
                        builder.Append(FormatConstant(options.DiscriminatorSemanticTag ?? 39UL));
                        builder.Append("(");
                        builder.Append(DiscriminatorLiteral(model));
                        builder.Append(")");
                        break;

                    case "IntKeyMap":
                        // DiscriminatorMemberConverter.MemberIndex is 0: the index is reserved for it.
                        builder.Append("0: ");
                        builder.Append(DiscriminatorLiteral(model));
                        break;

                    default:
                        builder.Append(TextLiteral("_t"));
                        builder.Append(": ");
                        builder.Append(DiscriminatorLiteral(model));
                        break;
                }

                builder.Append(",\n");
            }

            foreach (MemberModel member in InWireOrder(model))
            {
                string? reference = CddlTypeReference.Render(member.Type, byKey, referenceNames, options);

                if (reference is null)
                {
                    // Reported once per member, not once per rule: a type contributing both a bare and
                    // a -poly rule walks the same member list twice.
                    if (reportDiagnostics)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(
                            Diagnostics.NoCddlRepresentation,
                            model.Symbol.Locations.FirstOrDefault(),
                            member.Type.ToDisplayString(),
                            "'" + model.Symbol.Name + "." + member.Name + "'"));
                    }

                    continue;
                }

                builder.Append("  ");

                switch (model.ObjectFormat)
                {
                    case "Array":
                        // Positional: no key at all, just the value in wire order.
                        break;

                    case "IntKeyMap":
                        // Integer keys are never quoted -- unlike the text keys below, a decimal
                        // integer is unconditionally a legal bareword in CDDL's map-key position.
                        // Formatted via FormatConstant rather than StringBuilder's own Append(object)
                        // overload: CborIndex can be negative, and Append(object) would go through
                        // the current-culture ToString(), the same locale-dependent-negative-sign
                        // hazard EmitEnumRule's FormatConstant exists to avoid.
                        builder.Append(FormatConstant(member.CborIndex!.Value));
                        builder.Append(": ");
                        break;

                    default:
                        // Always quoted: a bareword member key is only legal CDDL when it matches
                        // RFC 8610's `id` production, and this library's naming conventions and
                        // [CborProperty("...")] both admit arbitrary strings (a leading digit, a
                        // space, ...) that `id` rejects. A quoted `tstr` key is unconditionally
                        // valid, so quoting removes the failure mode rather than trading it for a
                        // rarer one -- provided the name is escaped, which TextLiteral does.
                        builder.Append(TextLiteral(member.CborName));
                        builder.Append(": ");
                        break;
                }

                builder.Append(reference);
                builder.Append(",\n");
            }

            builder.Append(isArray ? "]" : "}");
        }

        /// <summary>
        /// ObjectMapping.ValidateMemberNamesAndindexes re-sorts IntKeyMap and Array members by
        /// ascending index at registration, so declared order is not wire order. For Array the schema
        /// is positional, which makes reproducing that sort a correctness requirement rather than a
        /// tidiness one. OrderBy is a stable sort, so members sharing an index (unreachable in
        /// practice -- ObjectMapping itself rejects duplicate indexes at registration) keep their
        /// declared order rather than depending on sort implementation, preserving byte-stability.
        /// </summary>
        private static IEnumerable<MemberModel> InWireOrder(TypeModel model)
        {
            if (model.ObjectFormat == "StringKeyMap")
            {
                return model.Members;
            }

            return model.Members.OrderBy(m => m.CborIndex ?? 0);
        }

        /// <summary>
        /// EnumConverter writes the underlying integer by default and the member name when EnumFormat
        /// is WriteToString. Naming the members rather than emitting a bare <c>int</c> is what makes
        /// an added enum member show up as a schema diff.
        /// </summary>
        /// <remarks>
        /// <see cref="System.FlagsAttribute"/> enums are the exception to "closed": a bitwise
        /// combination (<c>Colours.Red | Colours.Green</c>) is a value <c>EnumConverter&lt;T&gt;</c>
        /// writes unconditionally that need not equal any single declared member, so a rule closed
        /// over the declared values would reject the serializer's own output. Under the default
        /// format (<c>WriteInt32</c>) that means falling back to the open <c>uint</c>/<c>int</c>
        /// prelude type. Under <c>WriteToString</c>, <c>EnumConverter&lt;T&gt;.WriteString</c> looks
        /// the value up in a value-&gt;name dictionary built from the declared members only and, on a
        /// miss, itself falls back to <c>WriteInt32</c> -- so a flags enum in string mode can still
        /// write a bare integer for any combination that doesn't exactly match one declared member,
        /// and the schema has to admit both the named choices and the integer form.
        /// </remarks>
        private static void EmitEnumRule(
            StringBuilder builder,
            TypeModel model,
            IReadOnlyDictionary<string, string> ruleNames,
            GenerationOptions options)
        {
            string key = model.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            List<string> values = new List<string>();
            List<string> names = new List<string>();
            bool hasNegative = false;

            foreach (ISymbol member in model.Symbol.GetMembers())
            {
                if (member is IFieldSymbol { IsConst: true, ConstantValue: not null } field)
                {
                    string valueText = FormatConstant(field.ConstantValue);

                    if (valueText.Length > 0 && valueText[0] == '-')
                    {
                        hasNegative = true;
                    }

                    // Preserves first-seen order; an alias (`B = 1` alongside `A = 1`) would
                    // otherwise render as a noisy "1 / 1" in the choice/range form below.
                    if (!values.Contains(valueText))
                    {
                        values.Add(valueText);
                    }

                    // A C# identifier needs no escaping, but routing it through the same helper is what
                    // keeps TextLiteral the only place in this file that opens a quote.
                    names.Add(TextLiteral(field.Name));
                }
            }

            builder.Append(ruleNames[key]);
            builder.Append(" = ");

            bool isFlags = model.Symbol.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::System.FlagsAttribute");

            // A memberless enum has nothing to name and nothing to enumerate, so both the choice of
            // names and the choice of values would render empty -- and an empty choice arm (`E =  /
            // uint`, `E = `) is not parseable CDDL, which no amount of downstream validation can
            // recover from. Checked ahead of the flags branch, which builds a choice of its own.
            // `int` rather than the flags branch's `uint`: with no members there is nothing to read a
            // sign off, and only a cast can produce a value at all, so the signed prelude type is the
            // one that cannot reject what the serializer writes.
            if (values.Count == 0)
            {
                builder.Append("int");
            }
            else if (isFlags)
            {
                string integerForm = hasNegative ? "int" : "uint";

                builder.Append(options.EnumFormat == "WriteToString"
                    ? string.Join(" / ", names) + " / " + integerForm
                    : integerForm);
            }
            else if (options.EnumFormat == "WriteToString")
            {
                builder.Append(string.Join(" / ", names));
            }
            else
            {
                // A contiguous span of integers reads better as a range than as a long choice.
                builder.Append(IsContiguousFromZero(values)
                    ? "0.." + (values.Count - 1).ToString(CultureInfo.InvariantCulture)
                    : string.Join(" / ", values));
            }

            builder.Append("\n\n");
        }

        /// <summary>
        /// Enum member constants arrive as a boxed <c>sbyte</c>/<c>byte</c>/.../<c>ulong</c> (whatever
        /// the enum's underlying type is); all of those implement <see cref="System.IFormattable"/>.
        /// Formatting explicitly against <see cref="CultureInfo.InvariantCulture"/> -- rather than the
        /// culture-sensitive <c>object.ToString()</c> -- matters because .NET substitutes
        /// <see cref="System.Globalization.NumberFormatInfo.NegativeSign"/> for the ASCII '-' under
        /// some ICU locales (U+2212 MINUS SIGN), which would both emit invalid RFC 8610 and make the
        /// schema depend on the machine's locale rather than being a pure function of the source.
        /// </summary>
        private static string FormatConstant(object constantValue)
        {
            return ((System.IFormattable)constantValue).ToString(null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The discriminator as it goes into a CDDL rule: an integer verbatim, a string re-escaped from
        /// the raw text. <see cref="TypeModel.Discriminator"/> is a C# literal and is deliberately left
        /// alone -- <see cref="Emitter"/> pastes it into generated code, where it must stay valid C#.
        /// </summary>
        private static string DiscriminatorLiteral(TypeModel model)
        {
            return model.DiscriminatorText is null
                ? model.Discriminator!
                : TextLiteral(model.DiscriminatorText);
        }

        /// <summary>
        /// Quotes and escapes a string as an RFC 8610 text literal, whose escape alphabet is JSON's:
        /// <c>\"</c>, <c>\\</c>, <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c>, <c>\t</c>, and
        /// <c>\uXXXX</c> for every other character outside the grammar's <c>SCHAR</c> production.
        /// </summary>
        /// <remarks>
        /// Every quoted string in the schema goes through here -- member keys, which
        /// <c>[CborProperty("...")]</c> lets the user set to any string at all, and string
        /// discriminators. Emitting <c>"a"b"</c> for a key of <c>a"b</c> would not merely mis-describe
        /// the wire format: it is text no CDDL tool will parse, and a schema that does not parse
        /// cannot fail an instance check, so nothing downstream would notice.
        /// <para>
        /// Characters at or above U+0080 are left verbatim: <c>SCHAR</c> admits %x80-10FFFD, and
        /// escaping them would mean hand-rolling surrogate pairs for no gain.
        /// </para>
        /// </remarks>
        private static string TextLiteral(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 2);

            builder.Append('"');

            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;

                    default:
                        // SCHAR is %x20-21 / %x23-5B / %x5D-7E / %x80-10FFFD: the C0 controls and DEL
                        // are the only remaining characters it excludes.
                        if (character < ' ' || character == '\u007F')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');

            return builder.ToString();
        }

        private static bool IsContiguousFromZero(List<string> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] != index.ToString(CultureInfo.InvariantCulture))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Wraps the schema in the partial class that exposes it. A verbatim string rather than a raw
        /// string literal, because generated code compiles under the consuming project's LangVersion
        /// and raw strings require C# 11.
        /// </summary>
        /// <summary>
        /// Hint name for the schema file, sharing <see cref="TypeNames.HintNameStem"/> with the
        /// registration file so the two differ only in suffix and neither can collide with a
        /// same-named context in another namespace.
        /// </summary>
        public static string HintName(INamedTypeSymbol contextSymbol)
        {
            return TypeNames.HintNameStem(contextSymbol) + ".CddlSchema.g.cs";
        }

        public static string EmitSource(INamedTypeSymbol contextSymbol, string schema)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("// <auto-generated/>");
            builder.AppendLine("#nullable enable");
            builder.AppendLine();

            // Reopened through the same helper the converter half uses: this file is a second partial
            // declaration of the user's own context, so an internal, nested or generic context has to
            // be reopened exactly as it was declared.
            string indent = Emitter.OpenContextDeclaration(builder, contextSymbol);

            builder.AppendLine($"{indent}    /// <summary>RFC 8610 CDDL schema for the types declared on this context.</summary>");
            builder.AppendLine($"{indent}    public const string CddlSchema =");
            builder.AppendLine($"{indent}        @\"{schema.Replace("\"", "\"\"")}\";");

            Emitter.CloseContextDeclaration(builder, contextSymbol, indent);

            return builder.ToString();
        }
    }
}
