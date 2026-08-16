using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A required member and a <c>ShouldSerialize</c> predicate, both of which the delegate mappings
    /// have always been able to express and the generator simply did not emit.
    /// </summary>
    public class GeneratedRequiredHolder
    {
        [CborRequired]
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public class GeneratedShouldSerializeHolder
    {
        public int Id { get; set; }

        public string Name { get; set; }

        /// <summary>Skips <see cref="Name"/> exactly when it is empty, on both paths.</summary>
        public bool ShouldSerializeName() => !string.IsNullOrEmpty(Name);
    }

    [CborSerializable(typeof(GeneratedRequiredHolder))]
    [CborSerializable(typeof(GeneratedShouldSerializeHolder))]
    public partial class MemberFeatureContext : CborSerializerContext
    {
    }

    /// <summary>
    /// Both features change behaviour rather than bytes-for-a-given-value, so the corpus cannot see
    /// them: it compares one sample through both paths, and a sample that satisfies the requirement and
    /// passes the predicate encodes identically whether or not either is honoured.
    /// </summary>
    /// <remarks>
    /// What is asserted here is the part that only shows up on the values the feature exists for — a
    /// document missing the required member, and a value the predicate excludes — and in both cases
    /// against the reflection path, since agreeing with it is the contract.
    /// </remarks>
    public class GeneratedMemberFeatureTests
    {
        /// <summary>
        /// The requirement is a *read*-side rule, so it needs a document that omits the member. A
        /// generated context that dropped `SetRequired` would return a default-populated object here
        /// and say nothing.
        /// </summary>
        [Fact]
        public void ARequiredMemberIsEnforcedOnTheGeneratedPath()
        {
            MemberFeatureContext context = new MemberFeatureContext();

            // {"Name": "n"} -- Id absent
            const string missing = "A1644E616D65616E";

            Assert.Throws<CborException>(
                () => Cbor.Deserialize<GeneratedRequiredHolder>(missing.HexToBytes(), context.Options));

            // The reflection path refuses the same document, which is what makes this a match rather
            // than an opinion of the generated path's own.
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<GeneratedRequiredHolder>(missing.HexToBytes()));
        }

        /// <summary>A document that carries it reads back through the generated context.</summary>
        [Fact]
        public void ARequiredMemberThatIsPresentReadsBack()
        {
            MemberFeatureContext context = new MemberFeatureContext();
            GeneratedRequiredHolder value = new GeneratedRequiredHolder { Id = 7, Name = "n" };

            string generated = Helper.Write(value, context.Options);

            Assert.Equal(Helper.Write(value), generated, ignoreCase: true);
            Assert.Equal(7, Cbor.Deserialize<GeneratedRequiredHolder>(generated.HexToBytes(), context.Options).Id);
        }

        /// <summary>
        /// The predicate is a *write*-side rule, so it needs the value it excludes — which is exactly
        /// the value the corpus does not use.
        /// </summary>
        [Fact]
        public void AShouldSerializeMethodIsHonouredOnTheGeneratedPath()
        {
            MemberFeatureContext context = new MemberFeatureContext();
            GeneratedShouldSerializeHolder skipped = new GeneratedShouldSerializeHolder { Id = 1, Name = "" };

            string generated = Helper.Write(skipped, context.Options);

            // A map of one: Name is excluded by the predicate, so only Id is written.
            Assert.StartsWith("A1", generated, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Helper.Write(skipped), generated, ignoreCase: true);
        }

        [Fact]
        public void AShouldSerializeMethodThatPassesWritesTheMember()
        {
            MemberFeatureContext context = new MemberFeatureContext();
            GeneratedShouldSerializeHolder kept = new GeneratedShouldSerializeHolder { Id = 1, Name = "n" };

            string generated = Helper.Write(kept, context.Options);

            Assert.StartsWith("A2", generated, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Helper.Write(kept), generated, ignoreCase: true);
        }
    }
}
