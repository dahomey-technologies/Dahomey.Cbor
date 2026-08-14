using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Xunit;

// A polymorphic type contributes a second rule, named by appending `-poly` to its rule name, and that
// name is minted after rule names have been handed out. So a type whose own rule name is already the
// `-poly` form of another type's collides with it, and the collision is invisible to a uniqueness pass
// that only sees the declaration names.
//
// Reaching it needs a type named `poly` sitting where the qualified form of some polymorphic type is
// its prefix. A namespace and a type of the same name conflict in C#, so the pair cannot be built that
// way -- but a *nested* type sidesteps the conflict entirely: `Left.X.poly` qualifies through its
// containing type, giving exactly the base's qualified name plus `-poly`. Five ordinary types in one
// compilation, no cross-assembly trick.
//
// CS8981 is disabled for the two types named `poly`: the collision is against the literal suffix, so
// the name cannot be spelled any other way, and the warning is about names C# may one day reserve.
#pragma warning disable CS8981

namespace Dahomey.Cbor.Tests.Cddl.Left
{
    /// <summary>
    /// Contested with <see cref="Right.X"/>, so it takes the qualified form -- which is what makes its
    /// derived `-poly` name predictable enough to be collided with.
    /// </summary>
    public class X
    {
        public int Id { get; set; }

        /// <summary>
        /// Lower case deliberately: the collision is against the literal suffix, so the name has to be
        /// spelled the way <c>PolymorphicShape</c> spells it.
        /// </summary>
        public class poly
        {
            public int Value { get; set; }
        }
    }

    /// <summary>Gives <see cref="X"/> a subtype, which is what makes it emit a type choice at all.</summary>
    [CborDiscriminator("sub")]
    public class XSub : X
    {
        public int Extra { get; set; }
    }
}

namespace Dahomey.Cbor.Tests.Cddl.Right
{
    /// <summary>Contests the short name <c>X</c>, and nothing else.</summary>
    public class X
    {
        public int Id { get; set; }
    }

    /// <summary>Contests the short name <c>poly</c> with <see cref="Left.X.poly"/>.</summary>
    public class poly
    {
        public int Value { get; set; }
    }
}

namespace Dahomey.Cbor.Tests.Cddl
{
    [CborSerializable(typeof(Left.X))]
    [CborSerializable(typeof(Left.X.poly))]
    [CborSerializable(typeof(Left.XSub))]
    [CborSerializable(typeof(Right.X))]
    [CborSerializable(typeof(Right.poly))]
    [CborCddlSchema]
    public partial class CddlPolymorphicRuleNamingContext : CborSerializerContext
    {
    }

    /// <summary>
    /// The uniqueness guarantee covers the names a type is *emitted* under, not only the ones it asks
    /// for. A polymorphic type occupies two: its declaration name and the <c>-poly</c> form its type
    /// choice is emitted as, so both have to be reserved together or the second one is free for a
    /// declaration name to take.
    /// </summary>
    public class CddlPolymorphicRuleNamingTests
    {
        [Fact]
        public void ATypeNamedForThePolySuffixDoesNotTakeAPolymorphicTypesSecondRule()
        {
            string schema = CddlPolymorphicRuleNamingContext.CddlSchema.Replace("\r\n", "\n");

            Assert.Contains("Dahomey-Cbor-Tests-Cddl-Left-X-poly = ", schema);
            Assert.Contains("Dahomey-Cbor-Tests-Cddl-Left-X-poly-2 = {\n", schema);
        }

        [CddlFact]
        public void TheSchemaCarryingBothParses()
        {
            CddlResult result = CddlTool.Parse(CddlPolymorphicRuleNamingContext.CddlSchema);

            Assert.True(result.Ok, result.Output);
        }
    }
}
