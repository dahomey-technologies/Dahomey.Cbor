using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #233: a member a <c>ShouldSerialize</c> predicate declined was not written in
    /// <see cref="CborObjectFormat.Array"/>, and since that format emits no keys, every member after
    /// it slid one position earlier. The read resolved each position against the member list and
    /// landed on the wrong member: it threw where the shifted members disagreed on type, and assigned
    /// silently where they agreed.
    /// </summary>
    /// <remarks>
    /// The fix belongs to the writer. A reader cannot recover a position the document never carried,
    /// an omitted member leaving no trace to skip over, and the format has no way to express absence.
    /// So a member that holds a position is written whatever the predicate says — with its current
    /// value, which is the default in the very case <c>[CborIgnoreIfDefault]</c> exists to catch. The
    /// predicate still applies to the two map formats, where a key travels with each value.
    /// </remarks>
    public class Issue0233
    {
        [CborObjectFormat(CborObjectFormat.Array)]
        public class Skipper
        {
            [CborProperty(5)] public int Id { get; set; }
            [CborProperty(7)][CborIgnoreIfDefault] public string Name { get; set; }
            [CborProperty(9)] public int Tail { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class MapSkipper
        {
            [CborProperty(5)] public int Id { get; set; }
            [CborProperty(7)][CborIgnoreIfDefault] public string Name { get; set; }
            [CborProperty(9)] public int Tail { get; set; }
        }

        /// <summary>
        /// The reported shape: the declined member keeps its slot, written as the null its value is,
        /// and the array header still counts three items.
        /// </summary>
        [Fact]
        public void ADeclinedMemberStillHoldsItsPosition()
        {
            // 83 01 F6 182A  --  [1, null, 42]
            const string hexBuffer = "8301F6182A";

            Helper.TestWrite(new Skipper { Id = 1, Name = null, Tail = 42 }, hexBuffer);
        }

        [Fact]
        public void TheDocumentReadsBackOntoTheMembersItWasWrittenFrom()
        {
            Skipper skipper = Helper.Read<Skipper>("8301F6182A");

            Assert.NotNull(skipper);
            Assert.Equal(1, skipper.Id);
            Assert.Null(skipper.Name);
            Assert.Equal(42, skipper.Tail);
        }

        /// <summary>
        /// The other half of the fix: a map carries the key that says which member a value belongs
        /// to, so omitting a member costs nothing there and the predicate keeps working as declared.
        /// </summary>
        [Fact]
        public void AMapStillOmitsTheDeclinedMember()
        {
            // A2 05 01 09 182A  --  {5: 1, 9: 42}
            const string hexBuffer = "A2050109182A";

            Helper.TestWrite(new MapSkipper { Id = 1, Name = null, Tail = 42 }, hexBuffer);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class ShouldSerializeSkipper
        {
            [CborProperty(1)] public int Id { get; set; }
            [CborProperty(2)] public int Middle { get; set; }
            [CborProperty(3)] public int Tail { get; set; }

            public bool ShouldSerializeMiddle() => false;
        }

        /// <summary>
        /// The silent shape, and the one <c>ShouldSerializeXyz</c> reaches: three members of the same
        /// type, where a hole raises nothing and simply moves two values onto the wrong members.
        /// </summary>
        [Fact]
        public void AShouldSerializeMethodDoesNotShiftTheMembersAfterIt()
        {
            // 83 01 02 03  --  [1, 2, 3]
            const string hexBuffer = "83010203";

            Helper.TestWrite(new ShouldSerializeSkipper { Id = 1, Middle = 2, Tail = 3 }, hexBuffer);

            ShouldSerializeSkipper skipper = Helper.Read<ShouldSerializeSkipper>(hexBuffer);

            Assert.Equal(1, skipper.Id);
            Assert.Equal(2, skipper.Middle);
            Assert.Equal(3, skipper.Tail);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class ContradictorySkipper
        {
            [CborProperty(1)] public int Id { get; set; }

            [CborProperty(2)]
            [CborIgnoreIfDefault]
            [CborRequired(RequirementPolicy.DisallowNull)]
            public string Name { get; set; }
        }

        /// <summary>
        /// Writing the slot makes one contradictory pair of declarations visible: a member both
        /// omitted when default and required not to be null now says so, where the omission used to
        /// hide it behind a document that could not be read back.
        /// </summary>
        [Fact]
        public void ARequiredMemberThatWouldHaveBeenOmittedIsReported()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(new ContradictorySkipper { Id = 1, Name = null }));

            // Named by its index: an Array member carries no name to report.
            Assert.Equal("Property 'index 2' cannot be null.", exception.Message);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class PolymorphicBase
        {
            [CborProperty(1)] public int Id { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        [CborDiscriminator("issue233-derived")]
        public class PolymorphicDerived : PolymorphicBase
        {
            [CborProperty(2)] public int Tail { get; set; }
        }

        /// <summary>
        /// The discriminator holds no position and is not a member of the type, so the policy still
        /// decides whether it appears — including on a polymorphic write, where the member list comes
        /// from the derived type's converter rather than the declared type's.
        /// </summary>
        [Fact]
        public void TheDiscriminatorPolicyStillDecidesInAnArray()
        {
            CborOptions options = new CborOptions { DiscriminatorPolicy = CborDiscriminatorPolicy.Never };
            options.Registry.DiscriminatorConventionRegistry.RegisterType<PolymorphicDerived>();

            PolymorphicBase value = new PolymorphicDerived { Id = 1, Tail = 42 };

            // 82 01 182A  --  [1, 42], with no discriminator in front of it
            Assert.Equal("8201182A", Helper.Write(value, options));
        }

        [Fact]
        public void APresentValueIsUnaffected()
        {
            // 83 01 63 466F6F 182A  --  [1, "Foo", 42]
            const string hexBuffer = "830163466F6F182A";

            Helper.TestWrite(new Skipper { Id = 1, Name = "Foo", Tail = 42 }, hexBuffer);
        }
    }
}
