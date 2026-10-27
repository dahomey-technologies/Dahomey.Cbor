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

        /// <summary>
        /// A collection *interface* — <c>IList&lt;T&gt;</c>, <c>ICollection&lt;T&gt;</c>,
        /// <c>IEnumerable&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>,
        /// <c>IReadOnlyCollection&lt;T&gt;</c>, <c>ISet&lt;T&gt;</c> — which cannot be instantiated, so
        /// reading one builds a concrete backing collection and hands it back as the interface. That
        /// backing type is <see cref="BackingType"/>, chosen the way
        /// <c>CollectionConverterProvider</c> chooses it.
        /// </summary>
        InterfaceCollection,

        /// <summary>
        /// <c>IDictionary&lt;K,V&gt;</c> itself, whose backing type is always
        /// <c>Dictionary&lt;K,V&gt;</c> and so needs no <see cref="BackingType"/>.
        /// </summary>
        InterfaceDictionary,
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
        /// Whether the type carries <c>[CborObjectFormat]</c>, declared or inherited from a base
        /// class. Only then is the format stated in the generated registration: an undecorated type
        /// takes the options' format, as it does on the reflection path.
        /// </summary>
        public bool ObjectFormatDeclared { get; set; }

        /// <summary>
        /// The base class that declares the format, when it is inherited rather than declared on the
        /// type itself; null otherwise. Used to say so in a diagnostic.
        /// </summary>
        public string? ObjectFormatInheritedFrom { get; set; }

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
        /// The constructor a read builds this type through, when it cannot be built with
        /// <c>new T()</c> — a <c>[CborConstructor]</c>, or the single constructor of a type that has no
        /// parameterless one, which is what a positional record is.
        /// </summary>
        /// <remarks>
        /// Each entry is the parameter's type, the CBOR name of the member it takes its value from and
        /// that member's <c>[CborProperty(n)]</c> index (null for a member without one) and the
        /// constructor parameter's own name, in constructor order. <c>StringKeyMap</c> reads by the name and the other two formats by the index. The names
        /// are passed to <c>SetMemberNames</c> explicitly rather than left to the parameter names of the
        /// emitted lambda: a CBOR name set by <c>[CborProperty("my-name")]</c> or by a naming convention
        /// need not be a legal C# identifier, and a lambda's parameters have to be.
        /// </remarks>
        public List<(string Type, string CborName, int? Index, string ParameterName)> CreatorParameters { get; } =
            new List<(string Type, string CborName, int? Index, string ParameterName)>();

        /// <summary>
        /// True when the creator is emitted without member names, so the runtime matches each
        /// parameter itself. Needed under <c>StringKeyMap</c> for a parameter that reflection leaves
        /// at its default (its member is renamed and has a setter): a name cannot be listed for it,
        /// because listing a name that matches no member throws when the mapping is built. The runtime
        /// reads the names off the creator delegate's parameters, so the emitted lambda then carries
        /// the constructor's own parameter names instead of positional ones.
        /// </summary>
        public bool CreatorMatchesByName { get; set; }

        /// <summary>
        /// The concrete collection a <see cref="TypeKind.InterfaceCollection"/> is built into while it
        /// is read, before being handed back as the interface — <c>List&lt;T&gt;</c> for the ordered
        /// interfaces, <c>HashSet&lt;T&gt;</c> for <c>ISet&lt;T&gt;</c>.
        /// </summary>
        /// <remarks>
        /// Carried as a string rather than an <c>ITypeSymbol</c> because it is a type the source does
        /// not mention: nothing in the compilation refers to <c>List&lt;T&gt;</c> just because a member
        /// said <c>IList&lt;T&gt;</c>, so there is no symbol to reach for and the emitter only ever
        /// needs its name.
        /// </remarks>
        public string? BackingType { get; set; }

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
