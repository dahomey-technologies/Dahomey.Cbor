using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Renders the CDDL for a type as it appears at a use site. Objects resolve to a rule name;
    /// primitives are inlined. Every other kind -- enums included -- has no representation yet and
    /// returns null, so the caller can report CBOR1007.
    /// </summary>
    internal static class CddlTypeReference
    {
        /// <summary>
        /// Returns null when the type has no CDDL representation, so the caller can report CBOR1007
        /// with the member that reached it.
        /// </summary>
        public static string? Render(
            ITypeSymbol type,
            IReadOnlyDictionary<string, TypeModel> byKey,
            IReadOnlyDictionary<string, string> ruleNames,
            GenerationOptions options)
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
                    rendered = RenderPrimitive(type);
                    break;

                case TypeKind.Object:
                    rendered = ruleNames.TryGetValue(key, out string? name) ? name : null;
                    break;

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
            if (type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            {
                return rendered + " / nil";
            }

            return rendered;
        }

        /// <summary>
        /// Narrow integers get a range rather than the prelude's unbounded <c>int</c>, because the
        /// schema is a contract about what is written and a byte is never 256.
        /// </summary>
        private static string? RenderPrimitive(ITypeSymbol type)
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

                case SpecialType.System_Object:
                    return "any";
            }

            // System.Decimal is deliberately absent: it is written as 0xFC plus 16 bytes, and
            // additional information 28 is reserved and ill-formed under RFC 8949 section 3, so no
            // conforming decoder can read it and no CDDL can describe it. Guid, DateTimeOffset and
            // char have no scalar converter either. Nor does System.Half: the only place the library
            // references it is the RFC 8746 typed-array element path, which writes `#6.84(bstr)` --
            // a different representation entirely, not this method's concern. Each falls through to
            // CBOR1007 rather than asserting a row no converter backs.
            return null;
        }
    }
}
