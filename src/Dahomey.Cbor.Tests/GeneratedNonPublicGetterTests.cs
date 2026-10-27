using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    // A property is accepted or skipped by the reflection path on its GETTER: protected and private
    // ones are skipped, every other accessibility -- internal, protected internal, private protected --
    // is mapped, and [CborProperty] overrides the skip. The setter plays no part. The generated path
    // must skip the same properties; emitting `o.Name` for a getter it cannot call is CS0271.

    public class NonPublicGetterPrivate
    {
        public int Id { get; set; }
        public string Name { private get; set; }
        public string Peek() => Name;
    }

    public class NonPublicGetterProtected
    {
        public int Id { get; set; }
        public string Name { protected get; set; }
        public string Peek() => Name;
    }

    public class NonPublicGetterInternal
    {
        public int Id { get; set; }
        public string Name { internal get; set; }
    }

    public class NonPublicGetterProtectedInternal
    {
        public int Id { get; set; }
        public string Name { protected internal get; set; }
    }

    /// <summary>
    /// A skipped member is not mapped, so the <c>ShouldSerialize</c> beside it is never looked up:
    /// reflection does not throw for a static one here, and neither may the generator.
    /// </summary>
    public class NonPublicGetterBesideAStaticPredicate
    {
        public int Id { get; set; }
        public string Name { private get; set; }
        public static bool ShouldSerializeName() => false;
        public string Peek() => Name;
    }

    public class NonPublicGetterBase
    {
        public virtual string Name { protected get; set; }
    }

    /// <summary>The base's skipped property, inherited, and one overridden with the same getter.</summary>
    public class NonPublicGetterInherited : NonPublicGetterBase
    {
        public int Id { get; set; }
    }

    public class NonPublicGetterOverridden : NonPublicGetterBase
    {
        public int Id { get; set; }
        public override string Name { protected get => base.Name; set => base.Name = value; }
    }

    [CborSerializable(typeof(NonPublicGetterPrivate))]
    [CborSerializable(typeof(NonPublicGetterProtected))]
    [CborSerializable(typeof(NonPublicGetterInternal))]
    [CborSerializable(typeof(NonPublicGetterProtectedInternal))]
    [CborSerializable(typeof(NonPublicGetterBesideAStaticPredicate))]
    [CborSerializable(typeof(NonPublicGetterInherited))]
    [CborSerializable(typeof(NonPublicGetterOverridden))]
    public partial class NonPublicGetterContext : CborSerializerContext
    {
    }

    public class GeneratedNonPublicGetterTests
    {
        // Id = 1, Name = "n": both members, or Id alone.
        private const string BothWritten = "A262496401644E616D65616E";
        private const string IdOnly = "A162496401";

        private static void Agree<T>(T value, string expected)
        {
            NonPublicGetterContext context = new NonPublicGetterContext();

            Assert.Equal(expected, Helper.Write(value), ignoreCase: true);
            Assert.Equal(expected, Helper.Write(value, context.Options), ignoreCase: true);
        }

        [Fact] public void APrivateGetterIsSkipped() => Agree(new NonPublicGetterPrivate { Id = 1, Name = "n" }, IdOnly);
        [Fact] public void AProtectedGetterIsSkipped() => Agree(new NonPublicGetterProtected { Id = 1, Name = "n" }, IdOnly);
        [Fact] public void AnInternalGetterIsMapped() => Agree(new NonPublicGetterInternal { Id = 1, Name = "n" }, BothWritten);
        [Fact] public void AProtectedInternalGetterIsMapped() => Agree(new NonPublicGetterProtectedInternal { Id = 1, Name = "n" }, BothWritten);
        [Fact] public void ASkippedPropertyBesideAStaticPredicateIsStillSkipped() => Agree(new NonPublicGetterBesideAStaticPredicate { Id = 1, Name = "n" }, IdOnly);
        [Fact] public void AnInheritedSkippedPropertyIsSkipped() => Agree(new NonPublicGetterInherited { Id = 1, Name = "n" }, IdOnly);
        [Fact] public void AnOverriddenSkippedPropertyIsSkipped() => Agree(new NonPublicGetterOverridden { Id = 1, Name = "n" }, IdOnly);

        [Fact]
        public void AMappedInternalGetterReadsBackOnTheGeneratedPath()
        {
            NonPublicGetterContext context = new NonPublicGetterContext();

            NonPublicGetterInternal back = Cbor.Deserialize<NonPublicGetterInternal>(BothWritten.HexToBytes(), context.Options);

            Assert.Equal("n", back.Name);
        }

        // [CborProperty] makes the reflection path map a property whatever its getter is. The generated
        // path cannot call a protected, private or private protected one, so it reports CBOR1008 for
        // these (DiagnosticTests); what is pinned here is the reflection side of that claim.
        public class PrivateGetterRequested { public int Id { get; set; } [CborProperty("n")] public string Name { private get; set; } }
        public class ProtectedGetterRequested { public int Id { get; set; } [CborProperty("n")] public string Name { protected get; set; } }
        public class PrivateProtectedGetterRequested { public int Id { get; set; } [CborProperty("n")] public string Name { private protected get; set; } }
        public class PrivateProtectedGetter { public int Id { get; set; } public string Name { private protected get; set; } }

        [Fact]
        public void TheReflectionPathMapsAGetterThatIsRequested()
        {
            Assert.Equal("A262496401616E616E", Helper.Write(new PrivateGetterRequested { Id = 1, Name = "n" }), ignoreCase: true);
            Assert.Equal("A262496401616E616E", Helper.Write(new ProtectedGetterRequested { Id = 1, Name = "n" }), ignoreCase: true);
            Assert.Equal("A262496401616E616E", Helper.Write(new PrivateProtectedGetterRequested { Id = 1, Name = "n" }), ignoreCase: true);
        }

        /// <summary>
        /// <c>private protected</c> is neither <c>IsFamily</c> nor <c>IsPrivate</c>, so the reflection
        /// path maps it -- which the generator can only report, as no outside code can call it.
        /// </summary>
        [Fact]
        public void TheReflectionPathMapsAPrivateProtectedGetter()
        {
            Assert.Equal(BothWritten, Helper.Write(new PrivateProtectedGetter { Id = 1, Name = "n" }), ignoreCase: true);
        }
    }
}
