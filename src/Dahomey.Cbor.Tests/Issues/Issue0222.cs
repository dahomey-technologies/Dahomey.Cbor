using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #222: a <see cref="CborObjectFormat.Array"/> type whose declared indexes did not start at
    /// zero and run consecutively could not read its own output. The write emits members packed from
    /// the first position; the read counted positions and used the count as the declared index, so the
    /// two agreed only where the indexes happened to equal the positions.
    /// </summary>
    /// <remarks>
    /// An array carries no keys, so the index in <c>[CborProperty(n)]</c> orders the members and does
    /// not address them — <c>ObjectMapping</c> sorts by it and allows gaps and negatives alike, and
    /// <see cref="DeterministicEncodingTests"/> already pins a type declaring <c>-1</c>, which no
    /// position could ever be. The read now resolves a position against the member list and keys
    /// everything downstream on the index it finds there.
    /// <para>
    /// The silent shape is the one worth keeping covered: indexes far enough from the positions that
    /// nothing matched at all left every member defaulted and raised nothing, because an unmapped
    /// index is <see cref="UnhandledNameMode"/>'s business and it ignores by default.
    /// </para>
    /// </remarks>
    public class Issue0222
    {
        // 82 02 63 726F77  --  [2, "row"]
        private const string Row = "820263726F77";

        [CborObjectFormat(CborObjectFormat.Array)]
        public class ZeroBased
        {
            [CborProperty(0)] public int Id { get; set; }
            [CborProperty(1)] public string Name { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class OneBased
        {
            [CborProperty(1)] public int Id { get; set; }
            [CborProperty(2)] public string Name { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class Sparse
        {
            [CborProperty(5)] public int Id { get; set; }
            [CborProperty(9)] public string Name { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class Negative
        {
            [CborProperty(-1)] public int Id { get; set; }
            [CborProperty(4)] public string Name { get; set; }
        }

        /// <summary>
        /// The index set does not reach the wire: all four write the same two items, in ascending
        /// index order. Pinned first because it is what makes the read the only side that can be
        /// wrong, and what keeps this fix free of a format change.
        /// </summary>
        [Fact]
        public void EveryIndexSetWritesTheSameBytes()
        {
            Assert.Equal(Row, Helper.Write(new ZeroBased { Id = 2, Name = "row" }, new CborOptions()));
            Assert.Equal(Row, Helper.Write(new OneBased { Id = 2, Name = "row" }, new CborOptions()));
            Assert.Equal(Row, Helper.Write(new Sparse { Id = 2, Name = "row" }, new CborOptions()));
            Assert.Equal(Row, Helper.Write(new Negative { Id = 2, Name = "row" }, new CborOptions()));
        }

        [Fact]
        public void ZeroBasedIndexesRoundTrip()
        {
            ZeroBased row = Helper.Read<ZeroBased>(Row, new CborOptions());

            Assert.Equal(2, row.Id);
            Assert.Equal("row", row.Name);
        }

        /// <summary>
        /// Threw <c>Invalid major type TextString</c> at <c>$.Id</c>: position 0 was read as index 0,
        /// which no member holds, and position 1 as index 1, which is <c>Id</c> — so the text landed
        /// on the int.
        /// </summary>
        [Fact]
        public void OneBasedIndexesRoundTrip()
        {
            OneBased row = Helper.Read<OneBased>(Row, new CborOptions());

            Assert.Equal(2, row.Id);
            Assert.Equal("row", row.Name);
        }

        /// <summary>
        /// Returned an object with every member defaulted and raised nothing.
        /// </summary>
        [Fact]
        public void SparseIndexesRoundTrip()
        {
            Sparse row = Helper.Read<Sparse>(Row, new CborOptions());

            Assert.Equal(2, row.Id);
            Assert.Equal("row", row.Name);
        }

        /// <summary>
        /// A negative index is the case that settles what the index means: it cannot be a position,
        /// so ordering is all it can be.
        /// </summary>
        [Fact]
        public void NegativeIndexesRoundTrip()
        {
            Negative row = Helper.Read<Negative>(Row, new CborOptions());

            Assert.Equal(2, row.Id);
            Assert.Equal("row", row.Name);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        [CborDiscriminator("issue222-two-three")]
        public class DiscriminatedTwoBased
        {
            [CborProperty(2)] public int Id { get; set; }
            [CborProperty(3)] public string Name { get; set; }
        }

        /// <summary>
        /// The defect was never confined to undiscriminated types. A discriminated one whose members
        /// start at 2 rather than 1 failed identically, because the read reserved exactly one slot for
        /// the discriminator and then assumed the members carried on from there.
        /// </summary>
        [Fact]
        public void DiscriminatedIndexesNeedNotStartAtOne()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<DiscriminatedTwoBased>();

            Assert.Equal(Row, Helper.Write(new DiscriminatedTwoBased { Id = 2, Name = "row" }, options));

            DiscriminatedTwoBased row = Helper.Read<DiscriminatedTwoBased>(Row, options);

            Assert.Equal(2, row.Id);
            Assert.Equal("row", row.Name);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class RegistryBase
        {
            [CborProperty(1)] public int Id { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        [CborDiscriminator("issue222-registry-derived")]
        public class RegistryDerived : RegistryBase
        {
            [CborProperty(2)] public string Name { get; set; }
        }

        /// <summary>
        /// The read skipped a slot whenever a discriminator convention resolved for the type, which
        /// <see cref="Serialization.Conventions.DiscriminatorConventionRegistry"/> answers for every
        /// base of a registered hierarchy. So registering a derived type elsewhere in the program
        /// changed how the base alone read its own bytes, while the write was unaffected — the same
        /// document, the same type, two different results.
        /// </summary>
        [Fact]
        public void ReadingDoesNotDependOnAnotherTypeBeingRegistered()
        {
            const string justId = "810C"; // [12]

            CborOptions plain = new CborOptions();
            Assert.Equal(justId, Helper.Write(new RegistryBase { Id = 12 }, plain));
            Assert.Equal(12, Helper.Read<RegistryBase>(justId, plain).Id);

            CborOptions registered = new CborOptions();
            registered.Registry.DiscriminatorConventionRegistry.RegisterType<RegistryDerived>();
            Assert.Equal(justId, Helper.Write(new RegistryBase { Id = 12 }, registered));
            Assert.Equal(12, Helper.Read<RegistryBase>(justId, registered).Id);
        }

        /// <summary>
        /// A member the type can write but not read still holds its position: the writer emits it, so
        /// everything after it sits one place further along. It resolves to an index no read lookup
        /// holds, which skips the item without disturbing the count.
        /// </summary>
        [CborObjectFormat(CborObjectFormat.Array)]
        public class WithAGetOnlyMember
        {
            [CborProperty(3)] public int Id { get; set; }
            [CborProperty(6)] public string Computed => "computed";
            [CborProperty(8)] public string Name { get; set; }
        }

        [Fact]
        public void AMemberThatCannotBeReadStillHoldsItsPosition()
        {
            WithAGetOnlyMember value = new WithAGetOnlyMember { Id = 2, Name = "row" };

            // 83 02 68 636F6D7075746564 63 726F77  --  [2, "computed", "row"]
            const string hex = "830268636F6D7075746564" + "63726F77";
            Assert.Equal(hex, Helper.Write(value, new CborOptions()));

            WithAGetOnlyMember back = Helper.Read<WithAGetOnlyMember>(hex, new CborOptions());

            Assert.Equal(2, back.Id);
            Assert.Equal("row", back.Name);
        }

        /// <summary>
        /// More items than the type has members. There is no declared index to name, so the position
        /// is what gets reported.
        /// </summary>
        [Fact]
        public void AnArrayLongerThanTheTypeReportsThePosition()
        {
            // 83 02 63 726F77 0D  --  [2, "row", 13]
            const string threeItems = "830263726F770D";

            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };
            CborException exception = Assert.Throws<CborException>(
                () => Helper.Read<OneBased>(threeItems, options));

            Assert.Contains("Unhandled index [2]", exception.Message);

            // Ignored by default, leaving the members that did map alone.
            OneBased row = Helper.Read<OneBased>(threeItems, new CborOptions());
            Assert.Equal(2, row.Id);
            Assert.Equal("row", row.Name);
        }
    }
}
