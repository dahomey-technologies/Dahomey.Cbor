using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

// A same-named nested type in two different namespaces, chosen over the finding's literal
// "Envelope<Left.Item> vs Envelope<Right.Item>" example because TypeCollector.Classify only ever
// resolves a *generic* named type to Nullable<T>, an IDictionary<K,V>/ICollection<T> implementor, or
// Unsupported (Dahomey.Cbor.Generator/TypeCollector.cs, the branch guarded by
// "type is INamedTypeSymbol named && named.IsGenericType") -- a custom open generic POCO like
// Envelope<T> is CBOR1002 and cannot appear in an emitted object rule at all, generic or not. This
// fixture exercises the identical code path (TypeNames.BuildRuleNames / QualifiedAccessorName /
// QualifiedSimpleName) through the collision the finding names as an equivalent case: "Same for
// nested A.Inner and B.Inner in one namespace, since type.Name drops containing types."
namespace N
{
    public class Outer
    {
        public class Inner
        {
            public int Value { get; set; }
        }
    }

    public class Other
    {
        public class Inner
        {
            public int Value { get; set; }
        }
    }
}

namespace Dahomey.Cbor.Tests.Cddl
{
    // N.Other.Inner is reached transitively through this member rather than declared as its own
    // [CborSerializable] root, which keeps the fixture to the one collision it is about: routing one
    // of the pair in as a plain member still puts both types in the same context's TypeModel list,
    // which is all TypeNames.BuildRuleNames needs to collide on, without also exercising the separate
    // accessor-name collision that Emitter.UniqueAccessorName resolves for two same-short-named roots.
    public class CddlRuleNamingOtherHolder
    {
        public N.Other.Inner Value { get; set; }
    }

    [CborSerializable(typeof(N.Outer.Inner))]
    [CborSerializable(typeof(CddlRuleNamingOtherHolder))]
    [CborCddlSchema]
    public partial class CddlRuleNamingContext : CborSerializerContext
    {
    }

    /// <summary>
    /// <see cref="TypeNames.BuildRuleNames"/> buckets types by <see cref="TypeNames.AccessorName"/>,
    /// which is deliberately namespace- and containing-type-blind (it exists to build a readable
    /// short name, not a unique one -- it returns bare <c>type.Name</c> for a leaf type). Two
    /// same-named nested types in different namespaces collide in that short name --
    /// <c>N.Outer.Inner</c> and <c>N.Other.Inner</c> are both just <c>Inner</c> -- so
    /// <c>BuildRuleNames</c> must fall back to a namespace- and containing-type-qualified name for
    /// every member of the collision. Without that fallback the schema carries two rules under one
    /// name, which the gem accepts silently when their bodies are identical, the second shadowing the
    /// first.
    /// </summary>
    public class CddlRuleNamingTests
    {
        [Fact]
        public void CollidingNestedTypesGetDistinctRuleNames()
        {
            string schema = CddlRuleNamingContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("N-Outer-Inner = {\n", schema);
            Assert.Contains("N-Other-Inner = {\n", schema);
        }
    }
}
