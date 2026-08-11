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
    /// the point: the alternative is a document that silently drops a member. The check is on the
    /// mapped name, so it covers every route to the collision rather than the attribute alone: a
    /// naming convention that folds two member names into one, a mapping API call that maps a member
    /// twice, and a member that hides a base member of the same name all arrive at the same place with
    /// nothing in the source looking wrong.
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

        /// <summary>The type <see cref="TwoMembersOneName"/> becomes once the collision is removed.</summary>
        public class OneMemberOneName
        {
            [CborProperty("X")] public int First { get; set; }
        }

        /// <summary>Two members that are written and never read: the read lookup never sees either.</summary>
        public class SerializeOnlyCollision
        {
            [CborProperty("X")] public int First => 1;
            [CborProperty("X")] public int Second => 2;
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class DistinctIndexes
        {
            [CborProperty(1)] public int First { get; set; }
            [CborProperty(2)] public int Second { get; set; }
        }

        public class BaseWithAField { public int Value; }
        public class HidesAField : BaseWithAField { public new int Value; }

        public class BaseWithAProperty { public object Value { get; set; } }
        public class HidesAProperty : BaseWithAProperty { public new string Value { get; set; } }

        public class BaseWithAnInt { public int Value { get; set; } }
        public class HidesWithTheSameSignature : BaseWithAnInt { public new int Value { get; set; } }

        [CborDiscriminator("collides")]
        public class CollidesWithDiscriminator
        {
            [CborProperty("_t")] public int NotTheDiscriminator { get; set; }
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

        /// <summary>
        /// Asserts the refusal came from the validation of the whole mapping, rather than from the
        /// read lookup that catches what arrives after it.
        /// </summary>
        /// <remarks>
        /// The two report the same kind of failure, so without this a test cannot say which one
        /// answered — and they are not interchangeable: the read lookup is only ever offered the
        /// members that can be deserialized, so it cannot see a collision between members that are
        /// only written. Every test below that names a route into the collision pins the validation
        /// through this, leaving the two late-arrival tests to pin the other.
        /// </remarks>
        private static CborException AssertRefusedByValidation(Action action)
        {
            CborException exception = AssertThrowsCborException(action);
            Assert.DoesNotContain("after it was validated", exception.Message);
            return exception;
        }

        [Fact]
        public void WritingRefusesTheMapping()
        {
            TwoMembersOneName obj = new TwoMembersOneName { First = 1, Second = 2 };

            CborException ex = AssertRefusedByValidation(() => Helper.Write(obj));

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

            CborException first = AssertRefusedByValidation(
                () => Helper.Read<TwoMembersOneName>(hexBuffer, options));
            CborException second = AssertRefusedByValidation(
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

            CborException ex = AssertRefusedByValidation(() => Helper.Write(obj));

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

            CborException ex = AssertRefusedByValidation(
                () => Helper.Write(new DistinctNames(), options));

            Assert.Contains("'X'", ex.Message);
        }

        /// <summary>
        /// A member that hides a base member of the same name — <c>new</c> rather than
        /// <c>override</c> — is a fourth route, and the one with nothing attribute-shaped in the
        /// source at all.
        /// </summary>
        /// <remarks>
        /// Reflection reports both declarations, so both are mapped, under the one name. The two
        /// lookups differ in how much they fold: <c>GetProperties</c> drops a hiding property whose
        /// signature matches the hidden one exactly, which is why
        /// <see cref="HidesWithTheSameSignature"/> maps once and is written normally, while
        /// <c>GetFields</c> folds nothing at all — <see cref="HidesAField"/> is <c>int</c> over
        /// <c>int</c> and still collides. Refusing is what these shapes wrote before: the key twice,
        /// the hidden member unreadable.
        /// </remarks>
        [Fact]
        public void AMemberHidingABaseMemberIsRefused()
        {
            CborException field = AssertRefusedByValidation(() => Helper.Write(new HidesAField()));
            CborException property = AssertRefusedByValidation(() => Helper.Write(new HidesAProperty()));

            Assert.Contains("'Value'", field.Message);
            Assert.Contains("'Value'", property.Message);
        }

        /// <summary>The signature-preserving case reflection folds for us, mapped once and written.</summary>
        [Fact]
        public void HidingWithTheSameSignatureIsUnaffected()
        {
            HidesWithTheSameSignature obj = new HidesWithTheSameSignature { Value = 1 };

            // a1 65 56616c7565 01  -- {"Value": 1}
            Helper.TestWrite(obj, "A16556616C756501");
        }

        /// <summary>
        /// A collision between members that are written but never read is seen by the validation and
        /// by nothing else, so it is the shape that says the validation is load-bearing.
        /// </summary>
        /// <remarks>
        /// The read lookup refuses a key it already holds, which catches a collision that arrives too
        /// late for the validation — but it is only ever offered the members that can be deserialized.
        /// Get-only properties, readonly fields, consts and statics are written and never read, so
        /// they never reach it. Without the validation this writes <c>{"X": 1, "X": 2}</c> and says
        /// nothing, which is the bug the issue opened on.
        /// </remarks>
        [Fact]
        public void ASerializeOnlyCollisionIsRefused()
        {
            CborException ex = AssertRefusedByValidation(() => Helper.Write(new SerializeOnlyCollision()));

            Assert.Contains(nameof(SerializeOnlyCollision), ex.Message);
            Assert.Contains("'X'", ex.Message);
        }

        /// <summary>
        /// A member added to the mapping after something has already initialized it arrives past the
        /// check, at the read lookup, which refuses the key on its own terms. It reports as the same
        /// kind of failure — the library's own exception type, not the raw
        /// <see cref="System.ArgumentException"/> of the container that caught it — and says that it
        /// arrived late, which is what tells the two mechanisms apart.
        /// </summary>
        [Fact]
        public void ACollisionAddedAfterValidationReportsTheSameWay()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<DistinctNames>(objectMapping =>
            {
                objectMapping.AutoMap();

                // reading the mappings initializes them, so the validation has already run
                _ = objectMapping.MemberMappings.Count;

                objectMapping.MapMember(o => o.First);
            });

            CborException ex = AssertThrowsCborException(
                () => Helper.Write(new DistinctNames(), options));

            Assert.Contains(nameof(DistinctNames), ex.Message);
            Assert.Contains("'X'", ex.Message);
            Assert.Contains("after it was validated", ex.Message);
        }

        /// <summary>
        /// The same, in an integer-keyed format, where it is an index that repeats rather than a name.
        /// </summary>
        [Fact]
        public void ACollisionAddedAfterValidationReportsTheSameWayOnAnIndex()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<DistinctIndexes>(objectMapping =>
            {
                objectMapping.AutoMap();
                _ = objectMapping.MemberMappings.Count;
                objectMapping.MapMember(o => o.First);
            });

            CborException ex = AssertThrowsCborException(
                () => Helper.Write(new DistinctIndexes(), options));

            Assert.Contains(nameof(DistinctIndexes), ex.Message);
            Assert.Contains("MemberIndex", ex.Message);
            Assert.Contains("after it was validated", ex.Message);
        }

        /// <summary>
        /// The discriminator is a key of the map rather than a member of the type, and a member mapped
        /// onto its name collides with it exactly as two members collide with each other.
        /// </summary>
        [Fact]
        public void AMemberMappedOntoTheDiscriminatorNameIsRefused()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<CollidesWithDiscriminator>();

            CborException ex = AssertRefusedByValidation(
                () => Helper.Write(new CollidesWithDiscriminator(), options));

            Assert.Contains("'_t'", ex.Message);
        }

        /// <summary>
        /// The migration path the documentation offers: a document one of these mappings already wrote
        /// still reads with <see cref="DuplicateKeyMode.LastWins"/>, against a type whose mapping no
        /// longer collides. Last occurrence wins, which is what the assign path did before #169.
        /// </summary>
        [Fact]
        public void ALegacyDocumentStillReadsWithLastWins()
        {
            CborOptions options = new CborOptions { DuplicateKeyMode = DuplicateKeyMode.LastWins };

            // a2 6158 01 6158 02  -- {"X": 1, "X": 2}, written before the mapping was untangled
            OneMemberOneName obj = Helper.Read<OneMemberOneName>("A2615801615802", options);

            Assert.Equal(2, obj.First);
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
            CborException ex = AssertRefusedByValidation(
                () => Helper.Write(new TwoMembersOneIndex()));

            Assert.Contains(nameof(TwoMembersOneIndex), ex.Message);
            Assert.Contains("MemberIndex", ex.Message);
        }
    }
}
