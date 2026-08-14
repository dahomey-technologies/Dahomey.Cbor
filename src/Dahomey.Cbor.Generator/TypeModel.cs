using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Dahomey.Cbor.Generator
{
    /// <summary>How a type must be handled when emitting registrations.</summary>
    internal enum TypeKind
    {
        /// <summary>Resolved by a concrete converter with no generic instantiation — nothing to emit.</summary>
        Primitive,

        Enum,
        Nullable,
        Array,

        /// <summary>
        /// <c>byte[]</c>, which is a plain CBOR byte string rather than an array of small integers. Not an RFC 8746
        /// typed array either — see <see cref="IsTypedArray"/>.
        /// </summary>
        ByteArray,

        Collection,
        Dictionary,
        Object,

        /// <summary>
        /// A <c>ValueTuple</c>, which is one flat CBOR array of its elements. Kept apart from
        /// <see cref="Collection"/> because its elements are heterogeneous and its converter takes one
        /// type argument per element rather than a single element type.
        /// </summary>
        Tuple,

        /// <summary>Cannot be handled; the caller reports a diagnostic.</summary>
        Unsupported,
    }

    /// <summary>One member of an object, resolved to what the emitter needs.</summary>
    internal sealed class MemberModel
    {
        public MemberModel(
            string name,
            string cborName,
            int? cborIndex,
            ITypeSymbol type,
            bool canRead,
            bool canWrite)
        {
            Name = name;
            CborName = cborName;
            CborIndex = cborIndex;
            Type = type;
            CanRead = canRead;
            CanWrite = canWrite;
        }

        /// <summary>The C# member name, used to build the accessor lambdas.</summary>
        public string Name { get; }

        /// <summary>The wire name, after naming convention and <c>[CborProperty]</c>.</summary>
        public string CborName { get; }

        /// <summary>Set instead of <see cref="CborName"/> for IntKeyMap / Array formats.</summary>
        public int? CborIndex { get; }

        public ITypeSymbol Type { get; }

        public bool CanRead { get; }

        /// <summary>
        /// False for get-only and <c>init</c>-only members: <c>o.X = v</c> would not compile, so the
        /// mapping is emitted without a setter and the member is write-only.
        /// </summary>
        public bool CanWrite { get; }
    }

    /// <summary>A type reachable from a context declaration, and everything needed to emit it.</summary>
    internal sealed class TypeModel
    {
        public TypeModel(ITypeSymbol symbol, TypeKind kind)
        {
            Symbol = symbol;
            Kind = kind;
            Members = new List<MemberModel>();
            Dependencies = new List<ITypeSymbol>();
        }

        public ITypeSymbol Symbol { get; }

        public TypeKind Kind { get; }

        public List<MemberModel> Members { get; }

        /// <summary>
        /// Types whose converters must be registered before this one, because
        /// <c>ObjectConverter</c>/collection converters resolve their element and member converters
        /// during construction.
        /// </summary>
        public List<ITypeSymbol> Dependencies { get; }

        /// <summary>Object format, when <see cref="Kind"/> is <see cref="TypeKind.Object"/>.</summary>
        public string ObjectFormat { get; set; } = "StringKeyMap";

        /// <summary>
        /// A tuple's type arguments, when <see cref="Kind"/> is <see cref="TypeKind.Tuple"/>: one per
        /// element up to seven, and for a longer tuple the seven plus the <c>Rest</c> holding the
        /// overflow. These are the converter's type arguments, so the eighth is the <c>Rest</c>'s own
        /// type rather than an element -- <c>Tuple8Converter</c> recurses into it, which is what covers
        /// every arity with eight converters.
        /// </summary>
        public List<ITypeSymbol> TupleArguments { get; } = new List<ITypeSymbol>();

        /// <summary>Rendered C# literal for the discriminator, or null when the type has none.</summary>
        public string? Discriminator { get; set; }

        /// <summary>
        /// The raw text of a <c>[CborDiscriminator("...")]</c> value, unescaped and unquoted, or null
        /// when the discriminator is an integer or absent. <see cref="Discriminator"/> is a <em>C#</em>
        /// literal, which the registration emitter pastes into generated code; C# and RFC 8610 do not
        /// share an escape alphabet (<c>\a</c>, <c>\v</c>, <c>\0</c> and <c>\x..</c> are C# only), so
        /// the CDDL emitter escapes this raw text for itself rather than reusing that literal.
        /// </summary>
        public string? DiscriminatorText { get; set; }

        public string? DiscriminatorPolicy { get; set; }

        /// <summary>
        /// False for abstract classes and interfaces, which cannot be instantiated; the emitter omits
        /// the factory so <c>ObjectConverter</c> keeps its "needs a creator mapping" behaviour.
        /// </summary>
        public bool CanInstantiate { get; set; } = true;

        /// <summary>Element type for arrays and collections; key type for dictionaries.</summary>
        public ITypeSymbol? ElementType { get; set; }

        /// <summary>
        /// True when <see cref="Kind"/> is <see cref="TypeKind.Array"/> and the element type is one of
        /// the RFC 8746 typed array element types, so the emitter registers
        /// <c>TypedArrayConverter&lt;T&gt;</c> instead of <c>ArrayConverter&lt;T&gt;</c>. The reflection
        /// path reaches the same converter through <c>MakeGenericType</c>, which under Native AOT never
        /// emitted the closed generic; naming it in generated code is what makes it exist.
        /// </summary>
        public bool IsTypedArray { get; set; }

        /// <summary>Value type for dictionaries.</summary>
        public ITypeSymbol? ValueType { get; set; }

        /// <summary>Underlying type for <see cref="TypeKind.Nullable"/>.</summary>
        public ITypeSymbol? UnderlyingType { get; set; }
    }
}
