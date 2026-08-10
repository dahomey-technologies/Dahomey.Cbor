using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #177: a type mapping two members to one CBOR name wrote a document with the key repeated
    /// — one it could not read back — and nothing said so. The mapping is now refused when it is
    /// built, naming the type and the colliding name.
    /// </summary>
    /// <remarks>
    /// The ambiguity runs both ways and no reading of it is right: only one of the two members can
    /// ever be read from the key, and writing both is not representable. #169 made the resulting
    /// document a hard error on the way in, which left the library unable to read what it had just
    /// written; <see cref="DuplicateKeyMode.LastWins"/> reads such a document for anyone holding one
    /// already, but it is a way to live with the mapping rather than an answer to it.
    /// <para>
    /// Refusing at build time is a new failure at first use for a type that "worked" before, which is
    /// the point: the alternative is a document that silently drops a member. The check covers every
    /// route to the collision, since the attribute is only the most visible one — a naming convention
    /// that folds two member names into one, or a mapping API call that maps a member twice, arrives
    /// at the same place with nothing in the source looking wrong.
    /// </para>
    /// </remarks>
    public class Issue0177
    {
        public class TwoMembersOneName
        {
            [CborProperty("X")] public int First { get; set; }
            [CborProperty("X")] public int Second { get; set; }
        }

        /// <summary>Both members carry a name of their own; the convention is what folds them together.</summary>
        [CborNamingConvention(typeof(LowerCaseNamingConvention))]
        public class FoldedByNamingConvention
        {
            public int Id { get; set; }
            public int ID { get; set; }
        }

        public class DistinctNames
        {
            [CborProperty("X")] public int First { get; set; }
            [CborProperty("Y")] public int Second { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class TwoMembersOneIndex
        {
            [CborProperty(0)] public int First { get; set; }
            [CborProperty(0)] public int Second { get; set; }
        }

        /// <summary>
        /// Converters are built through <see cref="Activator"/>, so a <see cref="CborException"/>
        /// raised while building a mapping arrives wrapped in a
        /// <see cref="System.Reflection.TargetInvocationException"/>.
        /// </summary>
        private static CborException AssertThrowsCborException(Action action)
        {
            Exception exception = Assert.ThrowsAny<Exception>(action);

            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is CborException cborException)
                {
                    return cborException;
                }
            }

            throw new Xunit.Sdk.XunitException(
                $"Expected a {nameof(CborException)} somewhere in the exception chain, got: {exception}");
        }

        [Fact]
        public void WritingRefusesTheMapping()
        {
            TwoMembersOneName obj = new TwoMembersOneName { First = 1, Second = 2 };

            CborException ex = AssertThrowsCborException(() => Helper.Write(obj));

            Assert.Contains(nameof(TwoMembersOneName), ex.Message);
            Assert.Contains("'X'", ex.Message);
        }

        /// <summary>
        /// The mapping is refused whichever direction reaches it first, and a second attempt is
        /// refused the same way: initialization is only recorded once it has passed validation, so the
        /// type does not come out of a failed build looking usable.
        /// </summary>
        [Fact]
        public void ReadingRefusesTheMapping()
        {
            // a2 6158 01 6158 02  -- {"X": 1, "X": 2}, what the mapping used to write
            const string hexBuffer = "A2615801615802";

            CborOptions options = new CborOptions();

            CborException first = AssertThrowsCborException(
                () => Helper.Read<TwoMembersOneName>(hexBuffer, options));
            CborException second = AssertThrowsCborException(
                () => Helper.Read<TwoMembersOneName>(hexBuffer, options));

            Assert.Contains("'X'", first.Message);
            Assert.Equal(first.Message, second.Message);
        }

        /// <summary>
        /// The route the issue calls out as the one nothing in the source looks wrong for: two members
        /// named distinctly in C#, mapped onto one name by the convention.
        /// </summary>
        [Fact]
        public void ANamingConventionFoldingTwoMembersIsRefused()
        {
            FoldedByNamingConvention obj = new FoldedByNamingConvention();

            CborException ex = AssertThrowsCborException(() => Helper.Write(obj));

            Assert.Contains(nameof(FoldedByNamingConvention), ex.Message);
            Assert.Contains("'id'", ex.Message);
        }

        /// <summary>And mapping a member twice through the mapping API, which names it only once.</summary>
        [Fact]
        public void MappingTheSameMemberTwiceByApiIsRefused()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<DistinctNames>(objectMapping =>
            {
                objectMapping.AutoMap();
                objectMapping.MapMember(o => o.First);
            });

            CborException ex = AssertThrowsCborException(
                () => Helper.Write(new DistinctNames(), options));

            Assert.Contains("'X'", ex.Message);
        }

        /// <summary>
        /// Distinct names are left alone. The check is on the mapped name rather than the member name,
        /// so this is the side of it that would go unnoticed if it were wrong.
        /// </summary>
        [Fact]
        public void DistinctNamesAreUnaffected()
        {
            DistinctNames obj = new DistinctNames { First = 1, Second = 2 };

            // a2 6158 01 6159 02  -- {"X": 1, "Y": 2}
            Helper.TestWrite(obj, "A2615801615902");
        }

        /// <summary>
        /// The integer-keyed formats already refused two members under one index. That check is
        /// untouched — this asserts the name check did not displace it.
        /// </summary>
        [Fact]
        public void TwoMembersUnderOneIndexAreStillRefused()
        {
            CborException ex = AssertThrowsCborException(
                () => Helper.Write(new TwoMembersOneIndex()));

            Assert.Contains(nameof(TwoMembersOneIndex), ex.Message);
            Assert.Contains("MemberIndex", ex.Message);
        }
    }
}
