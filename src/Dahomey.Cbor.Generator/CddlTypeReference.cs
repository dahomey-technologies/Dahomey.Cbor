using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Globalization;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Renders the CDDL for a type as it appears at a use site. Objects and enums resolve to a rule
    /// name; primitives, byte arrays, nullables, arrays/collections and dictionaries are inlined,
    /// recursing into their element/value types. A kind with no representation (or an element/value
    /// type that itself has none) returns null, so the caller can report CBOR1011.
    /// </summary>
    internal static class CddlTypeReference
    {
        /// <summary>Where the rendered text is about to be placed, which decides whether nil is admitted.</summary>
        private enum Position
        {
            /// <summary>An ordinary use site: a member, a collection element or a dictionary value.</summary>
            Value,

            /// <summary>
            /// The key half of a map entry. RFC 8610's <c>memberkey</c> production is
            /// <c>type1 S ["^" S] "=&gt;"</c> -- a <em>type1</em>, not a full <c>type</c> -- so a
            /// <c>/</c> choice is a parse error there, and <c>{* tstr / nil =&gt; int}</c> is text no
            /// conformant tool will read. Suppressing nil rather than parenthesising it is also the
            /// semantically correct call: <c>Dictionary&lt;TKey,TValue&gt;</c> throws on a null key, so
            /// a nilable key would describe a document the serializer cannot produce.
            /// </summary>
            MapKey,

            /// <summary>
            /// The right-hand side of a rule emitted for a declared root type. A root arrives as a
            /// <c>typeof(...)</c> argument, which carries no nullable annotation at all, so the
            /// annotation-driven suffix would fire on every reference-typed root; object and enum roots
            /// already get a bare rule, and this keeps collection, array and dictionary roots
            /// consistent with them.
            /// </summary>
            Root,
        }

        /// <summary>
        /// Returns null when the type has no CDDL representation, so the caller can report CBOR1011
        /// with the member that reached it.
        /// </summary>
        public static string? Render(
            ITypeSymbol type,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> ruleNames,
            GenerationOptions options)
        {
            return Render(type, byKey, ruleNames, options, Position.Value);
        }

        /// <summary>
        /// Renders a declared root type as the right-hand side of a rule of its own, so that
        /// <c>[CborSerializable(typeof(List&lt;Person&gt;))]</c> yields <c>ListOfPerson = [* Person]</c>
        /// rather than a schema describing only <c>Person</c>.
        /// </summary>
        public static string? RenderRoot(
            ITypeSymbol type,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> ruleNames,
            GenerationOptions options)
        {
            return Render(type, byKey, ruleNames, options, Position.Root);
        }

        private static string? Render(
            ITypeSymbol type,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> ruleNames,
            GenerationOptions options,
            Position position)
        {
            string key = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (!byKey.TryGetValue(key, out TypeModel? model))
            {
                return null;
            }

            string? rendered;

            switch (model.Kind)
            {
                case TypeKind.Primitive:
                    rendered = RenderPrimitive(type, options);
                    break;

                case TypeKind.Enum:
                case TypeKind.Object:
                    rendered = ruleNames.TryGetValue(key, out string? name) ? name : null;
                    break;

                case TypeKind.ByteArray:
                    rendered = "bstr";
                    break;

                case TypeKind.Nullable:
                {
                    string? underlying = Render(
                        UnderlyingOf(type, model), byKey, ruleNames, options, position);

                    rendered = underlying is null
                        ? null
                        // A `Nullable<T>` key is not nilable either, for the same two reasons MapKey
                        // documents: `{* 0..255 / nil => tstr}` does not parse, and a null key throws.
                        : position == Position.MapKey ? underlying : underlying + " / nil";
                    break;
                }

                case TypeKind.Array:
                    if (model.IsTypedArray && options.WritesTypedArrays)
                    {
                        ulong? tag = LittleEndianTypedArrayTag(model.ElementType!);
                        rendered = tag is null ? null : "#6." + tag.Value.ToString(CultureInfo.InvariantCulture) + "(bstr)";
                        break;
                    }

                    goto case TypeKind.Collection;

                case TypeKind.Collection:
                {
                    string? element = Render(
                        ElementOf(type, model), byKey, ruleNames, options, Position.Value);
                    rendered = element is null ? null : "[* " + element + "]";
                    break;
                }

                case TypeKind.Dictionary:
                {
                    string? dictionaryKey = Render(
                        ElementOf(type, model), byKey, ruleNames, options, Position.MapKey);
                    string? value = Render(
                        ValueOf(type, model), byKey, ruleNames, options, Position.Value);
                    rendered = dictionaryKey is null || value is null
                        ? null
                        : "{* " + dictionaryKey + " => " + value + "}";
                    break;
                }

                // A fixed, heterogeneous array: one entry per element, in order, with the Rest chain
                // flattened because that is what the writer emits -- a nine-element tuple is nine items.
                // Not `[* X]`, which would say any length of one type.
                case TypeKind.Tuple:
                {
                    List<string> elements = new List<string>();

                    foreach (ITypeSymbol element in TypeCollector.FlattenTupleElements((INamedTypeSymbol)type))
                    {
                        string? renderedElement = Render(element, byKey, ruleNames, options, Position.Value);

                        if (renderedElement is null)
                        {
                            return null;
                        }

                        elements.Add(renderedElement);
                    }

                    rendered = "[" + string.Join(", ", elements) + "]";
                    break;
                }

                default:
                    return null;
            }

            if (rendered is null)
            {
                return null;
            }

            // `any` already admits nil (it is the universal type), so appending "/ nil" would only
            // be redundant, not wrong -- skip it rather than emit `any / nil`.
            if (rendered == "any")
            {
                return rendered;
            }

            // A reference-type use site accepts nil, because Dahomey writes F6 for a null reference
            // member: a bare rule would be a schema that rejects the serializer's own output. Value
            // types cannot be null, so they are unaffected; NotAnnotated is the only annotation that
            // promises the reference is never null.
            if (position == Position.Value
                && type.IsReferenceType
                && type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            {
                return rendered + " / nil";
            }

            return rendered;
        }

        // The three accessors below read the element, value and underlying types off the symbol at
        // hand rather than off the shared TypeModel. TypeCollector keys its model table on a display
        // string that drops the nullable-reference modifier -- deliberately, because the registration
        // emitter must not emit two RegisterConverter calls for the one runtime type -- so
        // `List<string>` and `List<string?>` share a model, and whichever was collected first would
        // otherwise decide the element nilability of both. The model stays the fallback for the shapes
        // Classify recognises through an interface rather than through the type's own arity.

        private static ITypeSymbol ElementOf(ITypeSymbol type, TypeModel model)
        {
            if (type is IArrayTypeSymbol array)
            {
                return array.ElementType;
            }

            return type is INamedTypeSymbol { TypeArguments.Length: > 0 } named
                ? named.TypeArguments[0]
                : model.ElementType!;
        }

        private static ITypeSymbol ValueOf(ITypeSymbol type, TypeModel model)
        {
            return type is INamedTypeSymbol { TypeArguments.Length: > 1 } named
                ? named.TypeArguments[1]
                : model.ValueType!;
        }

        private static ITypeSymbol UnderlyingOf(ITypeSymbol type, TypeModel model)
        {
            return type is INamedTypeSymbol { TypeArguments.Length: > 0 } named
                ? named.TypeArguments[0]
                : model.UnderlyingType!;
        }

        /// <summary>
        /// Narrow integers get a range rather than the prelude's unbounded <c>int</c>, because the
        /// schema is a contract about what is written and a byte is never 256.
        /// </summary>
        private static string? RenderPrimitive(ITypeSymbol type, GenerationOptions options)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    return "bool";

                case SpecialType.System_Byte:
                    return "0..255";

                case SpecialType.System_SByte:
                    return "-128..127";

                case SpecialType.System_Int16:
                    return "-32768..32767";

                case SpecialType.System_UInt16:
                    return "0..65535";

                case SpecialType.System_Int32:
                    return "-2147483648..2147483647";

                case SpecialType.System_UInt32:
                    return "0..4294967295";

                case SpecialType.System_Int64:
                    return "int";

                case SpecialType.System_UInt64:
                    return "uint";

                // CborWriter.WriteSingle and WriteDouble both emit the shortest form that round-trips,
                // unconditionally, so a double may arrive on the wire as float16, float32 or float64.
                // The prelude's `float` is the only member of the family that accepts all three.
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return "float";

                case SpecialType.System_String:
                    return "tstr";

                // CharConverter writes a char through CborWriter.WriteChar, which UTF-8 encodes the
                // single character and writes it as a text string -- not as an integer code point.
                case SpecialType.System_Char:
                    return "tstr";

                case SpecialType.System_Object:
                    return "any";

                case SpecialType.System_DateTime:
                    // DateTimeConverter tags every form: 0 with an ISO 8601 text string, 1 with
                    // seconds since the epoch as an integer, and 1 with fractional seconds as a
                    // float for milliseconds.
                    return options.DateTimeFormat switch
                    {
                        "Unix" => "#6.1(int)",
                        "UnixMilliseconds" => "#6.1(float)",
                        _ => "#6.0(tstr)",
                    };

                // Describable only in the RFC 8949 section 3.4.4 form. The default encoding is major
                // type 7 with additional information 28, which section 3 reserves, so there is no CDDL
                // for it and a context leaving DecimalFormat alone still falls through to CBOR1011
                // below. The mantissa is the same three-way choice BigInteger renders as, and for the
                // same reason: WriteBigInteger only reaches for a bignum tag past 64 bits, and a
                // 96-bit mantissa does reach past it.
                case SpecialType.System_Decimal:
                    return options.WritesDecimalFractions
                        ? "#6.4([int, (int / #6.2(bstr) / #6.3(bstr))])"
                        : null;
            }

            // BigInteger has no SpecialType, so it is matched by name -- as TypeCollector.IsPrimitive
            // does, which is what lets it reach this method at all. CborWriter.WriteBigInteger emits a
            // basic integer whenever the value fits the ulong-bounded header (which reaches -2^64 on
            // the negative side) and only falls back to the RFC 8949 section 3.4.3 bignum tags beyond
            // it, so all three forms belong in the choice. The parentheses matter: RFC 8610's
            // memberkey production takes a type1, not a type, so a bare `/` choice cannot appear
            // there -- `{* int / #6.2(bstr) => tstr}` is a parse error, while a parenthesised type is
            // a type2 and is legal in every position this rendering can land in.
            if (type.ToDisplayString() == "System.Numerics.BigInteger")
            {
                return "(int / #6.2(bstr) / #6.3(bstr))";
            }

            // The two RFC 8949 section 3.4.4 types, matched by name for the same reason. Both write
            // their tag unconditionally over a two-element array, exponent first, and their mantissa
            // through WriteBigInteger -- so the mantissa is the same three-way choice as above, for the
            // same reason, and the exponent is a plain int because CborWriter.WriteInt32 emits one.
            // A `decimal` under DecimalFormat.DecimalFraction renders identically to
            // CborDecimalFraction, which is correct rather than a collision: the two write the same
            // bytes, and the declared type is what decides which converter produces them.
            switch (type.ToDisplayString())
            {
                case "Dahomey.Cbor.CborDecimalFraction":
                    return "#6.4([int, (int / #6.2(bstr) / #6.3(bstr))])";

                case "Dahomey.Cbor.CborBigFloat":
                    return "#6.5([int, (int / #6.2(bstr) / #6.3(bstr))])";

                // RFC 9562's binary UUID. Always tagged and always sixteen bytes, so no option
                // reaches it and the size is part of the schema.
                case "System.Guid":
                    return "#6.37(bstr .size 16)";
            }

            // DateTimeOffset has no scalar converter. Nor does System.Half: the only place
            // the library references it is the RFC 8746 typed-array element path, which writes
            // `#6.84(bstr)` -- a different representation entirely, not this method's concern.
            // System.Decimal reaches here only in its default encoding, which is likewise
            // indescribable. Each falls through to CBOR1011 rather than asserting a row no converter
            // backs.
            return null;
        }

        /// <summary>
        /// The little-endian half of the RFC 8746 tag table.
        /// </summary>
        /// <remarks>
        /// MATCHED PAIR: these numbers duplicate <c>TypedArrayTags</c> in
        /// <c>src/Dahomey.Cbor/Serialization/Converters/TypedArrayTags.cs</c>. They cannot be shared --
        /// the generator is an analyzer assembly and must not reference the runtime library -- so the
        /// two must be edited together. <c>CddlTypedArrayTests.EveryTypedArrayTagIsEmitted</c> names
        /// every number as a literal, which is what catches an edit applied to both copies at once;
        /// comparing the two paths against each other cannot.
        /// <para>
        /// <c>byte</c> is deliberately absent: <c>byte[]</c> is a plain CBOR byte string.
        /// </para>
        /// </remarks>
        private static ulong? LittleEndianTypedArrayTag(ITypeSymbol element)
        {
            switch (element.SpecialType)
            {
                case SpecialType.System_SByte: return 72;
                case SpecialType.System_UInt16: return 69;
                case SpecialType.System_Int16: return 77;
                case SpecialType.System_UInt32: return 70;
                case SpecialType.System_Int32: return 78;
                case SpecialType.System_UInt64: return 71;
                case SpecialType.System_Int64: return 79;
                case SpecialType.System_Single: return 85;
                case SpecialType.System_Double: return 86;
            }

            return element.ToDisplayString() == "System.Half" ? 84 : (ulong?)null;
        }
    }
}
