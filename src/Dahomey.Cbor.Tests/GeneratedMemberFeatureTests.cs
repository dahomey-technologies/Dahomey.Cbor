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

    /// <summary>
    /// Both a constructor argument and a named one: the property is applied after the constructor, so
    /// the named <c>DisallowNull</c> is the policy, not the <c>AllowNull</c> written first.
    /// </summary>
    public class RequiredNamedPolicyHolder
    {
        public int Id { get; set; }

        [CborRequired(RequirementPolicy.AllowNull, Policy = RequirementPolicy.DisallowNull)]
        public string Name { get; set; }
    }

    /// <summary>A policy the enum does not name: reflection keeps the raw number, which behaves as Never.</summary>
    public class RequiredUndefinedConstructorPolicyHolder
    {
        public int Id { get; set; }

        [CborRequired((RequirementPolicy)7)]
        public string Name { get; set; }
    }

    public class RequiredNegativeNamedPolicyHolder
    {
        public int Id { get; set; }

        [CborRequired(Policy = (RequirementPolicy)(-1))]
        public string Name { get; set; }
    }

    [CborSerializable(typeof(RequiredUndefinedConstructorPolicyHolder))]
    [CborSerializable(typeof(RequiredNegativeNamedPolicyHolder))]
    [CborSerializable(typeof(GeneratedRequiredHolder))]
    [CborSerializable(typeof(RequiredNamedPolicyHolder))]
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

        /// <summary>
        /// DisallowNull does not require the member, so a document without it reads; one carrying an
        /// explicit null refuses, and so does writing a null. AllowNull would do the opposite on the
        /// last two, which is how a generator that ignored the named argument shows.
        /// </summary>
        [Fact]
        public void ANamedPolicyOverridesTheConstructorArgument()
        {
            MemberFeatureContext context = new MemberFeatureContext();

            // {"Id": 1} -- Name absent: not required under DisallowNull
            const string absent = "A1624964" + "01";
            Assert.Equal(1, Cbor.Deserialize<RequiredNamedPolicyHolder>(absent.HexToBytes()).Id);
            Assert.Equal(1, Cbor.Deserialize<RequiredNamedPolicyHolder>(absent.HexToBytes(), context.Options).Id);

            // {"Name": null} -- present but null: refused under DisallowNull
            const string nullName = "A1644E616D65F6";
            Assert.Throws<CborException>(() => Cbor.Deserialize<RequiredNamedPolicyHolder>(nullName.HexToBytes()));
            Assert.Throws<CborException>(() => Cbor.Deserialize<RequiredNamedPolicyHolder>(nullName.HexToBytes(), context.Options));

            RequiredNamedPolicyHolder withNull = new RequiredNamedPolicyHolder { Id = 1, Name = null };
            Assert.Throws<CborException>(() => Helper.Write(withNull));
            Assert.Throws<CborException>(() => Helper.Write(withNull, context.Options));
        }

        /// <summary>The result of an operation, or the type of what it threw, so two paths compare as values.</summary>
        private static string Outcome(System.Func<object> operation)
        {
            try
            {
                return operation() switch
                {
                    byte[] bytes => System.BitConverter.ToString(bytes),
                    object other => System.Text.Json.JsonSerializer.Serialize(other, other.GetType()),
                };
            }
            catch (System.Exception exception)
            {
                return "throws " + exception.GetType().Name;
            }
        }

        private static void AssertPathsAgreeOnAnUndefinedPolicy<T>(T withNull)
            where T : class
        {
            MemberFeatureContext context = new MemberFeatureContext();

            // {"Id": 1}: absent
            const string absent = "A1624964" + "01";
            // {"Name": null}: present and null
            const string nullName = "A1644E616D65F6";

            Assert.Equal(
                Outcome(() => Cbor.Deserialize<T>(absent.HexToBytes())),
                Outcome(() => Cbor.Deserialize<T>(absent.HexToBytes(), context.Options)));
            Assert.Equal(
                Outcome(() => Cbor.Deserialize<T>(nullName.HexToBytes())),
                Outcome(() => Cbor.Deserialize<T>(nullName.HexToBytes(), context.Options)));
            Assert.Equal(
                Outcome(() => Helper.Write(withNull)),
                Outcome(() => Helper.Write(withNull, context.Options)));
        }

        /// <summary>
        /// An undefined policy is not Always: both of these documents read and write on the reflection
        /// path, and folding the number onto a named member would refuse one of them.
        /// </summary>
        [Fact]
        public void AnUndefinedConstructorPolicyBehavesAsOnReflection()
        {
            AssertPathsAgreeOnAnUndefinedPolicy(new RequiredUndefinedConstructorPolicyHolder { Id = 1, Name = null });
            Assert.Equal(1, Cbor.Deserialize<RequiredUndefinedConstructorPolicyHolder>(("A1624964" + "01").HexToBytes()).Id);
        }

        [Fact]
        public void AnUndefinedNegativeNamedPolicyBehavesAsOnReflection()
        {
            AssertPathsAgreeOnAnUndefinedPolicy(new RequiredNegativeNamedPolicyHolder { Id = 1, Name = null });
            Assert.Equal(1, Cbor.Deserialize<RequiredNegativeNamedPolicyHolder>(("A1624964" + "01").HexToBytes()).Id);
        }
    }
}

