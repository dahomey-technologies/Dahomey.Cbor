using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Dahomey.Cbor.Util;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #169: whether a document repeating a key was refused depended on what it was being read
    /// into. Three targets rejected; a mapped class with settable members silently took the last
    /// occurrence, so the same class changed behaviour when someone added or removed a constructor.
    /// </summary>
    /// <remarks>
    /// RFC 8949 §5.6 declines to settle this — it requires a protocol to define what happens on
    /// repeated keys and leaves rejecting, first-wins and last-wins all open to the decoder — so the
    /// answer is a library policy rather than a conformance result. The policy is now reject
    /// everywhere, with <see cref="DuplicateKeyMode.LastWins"/> as the opt-out for
    /// protocols that define last-wins and for upgrading from a version where the assign path behaved
    /// that way unconditionally.
    /// <para>
    /// Both modes are asserted against every target, in both directions. The mode existing but
    /// applying to only some targets would be the same target-dependence wearing a different hat, and
    /// a matrix this small is exactly where that would rot unseen.
    /// </para>
    /// </remarks>
    public class Issue0169
    {
        // a2 6161 01 6161 02  -- {"a": 1, "a": 2}
        private const string DuplicateLowerA = "A2616101616102";

        // a2 6141 01 6141 02  -- {"A": 1, "A": 2}
        private const string DuplicateMemberA = "A2614101614102";

        // a2 624964 0c 624964 0d  -- {"Id": 12, "Id": 13}
        private const string DuplicateId = "A26249640C6249640D";

        // a2 00 0c 00 0d  -- {0: 12, 0: 13}
        private const string DuplicateIndexZero = "A2000C000D";

        private static CborOptions LastWins => new CborOptions { DuplicateKeyMode = DuplicateKeyMode.LastWins };

        public class Holder
        {
            public int A { get; set; }
        }

        public struct HolderStruct
        {
            public int A { get; set; }
        }

        public class ConstructedHolder
        {
            public int Id { get; set; }

            [CborConstructor]
            public ConstructedHolder(int id)
            {
                Id = id;
            }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class IndexedHolder
        {
            [CborProperty(0)]
            public int Id { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class IndexedConstructedHolder
        {
            [CborProperty(0)]
            public int Id { get; set; }

            [CborConstructor]
            public IndexedConstructedHolder(int id)
            {
                Id = id;
            }
        }

        public class RequiredHolder
        {
            [CborRequired]
            public int A { get; set; }
        }

        /// <summary>
        /// More members than fit in the bitmask "already read" is tracked in, so that the fallback for
        /// wider types is exercised rather than assumed.
        /// </summary>
        public class WideHolder
        {
            public int P0 { get; set; }
            public int P1 { get; set; }
            public int P2 { get; set; }
            public int P3 { get; set; }
            public int P4 { get; set; }
            public int P5 { get; set; }
            public int P6 { get; set; }
            public int P7 { get; set; }
            public int P8 { get; set; }
            public int P9 { get; set; }
            public int P10 { get; set; }
            public int P11 { get; set; }
            public int P12 { get; set; }
            public int P13 { get; set; }
            public int P14 { get; set; }
            public int P15 { get; set; }
            public int P16 { get; set; }
            public int P17 { get; set; }
            public int P18 { get; set; }
            public int P19 { get; set; }
            public int P20 { get; set; }
            public int P21 { get; set; }
            public int P22 { get; set; }
            public int P23 { get; set; }
            public int P24 { get; set; }
            public int P25 { get; set; }
            public int P26 { get; set; }
            public int P27 { get; set; }
            public int P28 { get; set; }
            public int P29 { get; set; }
            public int P30 { get; set; }
            public int P31 { get; set; }
            public int P32 { get; set; }
            public int P33 { get; set; }
            public int P34 { get; set; }
            public int P35 { get; set; }
            public int P36 { get; set; }
            public int P37 { get; set; }
            public int P38 { get; set; }
            public int P39 { get; set; }
            public int P40 { get; set; }
            public int P41 { get; set; }
            public int P42 { get; set; }
            public int P43 { get; set; }
            public int P44 { get; set; }
            public int P45 { get; set; }
            public int P46 { get; set; }
            public int P47 { get; set; }
            public int P48 { get; set; }
            public int P49 { get; set; }
            public int P50 { get; set; }
            public int P51 { get; set; }
            public int P52 { get; set; }
            public int P53 { get; set; }
            public int P54 { get; set; }
            public int P55 { get; set; }
            public int P56 { get; set; }
            public int P57 { get; set; }
            public int P58 { get; set; }
            public int P59 { get; set; }
            public int P60 { get; set; }
            public int P61 { get; set; }
            public int P62 { get; set; }
            public int P63 { get; set; }
            public int P64 { get; set; }
            public int P65 { get; set; }
        }

        #region Reject, which is the default

        [Fact]
        public void TheObjectModelRejectsADuplicate()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<CborObject>(DuplicateLowerA.HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        [Fact]
        public void ADictionaryRejectsADuplicate()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>(DuplicateLowerA.HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        /// <summary>
        /// A type with a creator mapping, whose member values are collected into a dictionary until the
        /// constructor can be called. Rejecting here is what #167 standardised.
        /// </summary>
        [Fact]
        public void AConstructedTypeRejectsADuplicate()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<ConstructedHolder>(DuplicateId.HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        /// <summary>
        /// The row this issue is about: a type whose members are assigned rather than collected. It
        /// took the last occurrence silently for the library's whole life, which is the behaviour break
        /// the release note names.
        /// </summary>
        [Fact]
        public void AMappedClassRejectsADuplicate()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Holder>(DuplicateMemberA.HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        /// <summary>A struct assigns its members through a separate path, which has to agree.</summary>
        [Fact]
        public void AStructRejectsADuplicate()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<HolderStruct>(DuplicateMemberA.HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        [Fact]
        public void AnIntKeyedMappedClassRejectsADuplicate()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IndexedHolder>(DuplicateIndexZero.HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        [Fact]
        public void AnIntKeyedConstructedTypeRejectsADuplicate()
        {
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<IndexedConstructedHolder>(DuplicateIndexZero.HexToBytes()));
        }

        /// <summary>
        /// The message says which member and where, like every other read failure: a caller rejecting
        /// a frame needs to be able to say what was wrong with it.
        /// </summary>
        [Fact]
        public void TheFailureNamesTheMemberAndItsPath()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Holder>(DuplicateMemberA.HexToBytes()));

            Assert.Equal("$.A", exception.Path);
            Assert.Contains("Duplicate map key", exception.Message);
            Assert.Contains("A", exception.Message);
        }

        /// <summary>
        /// Distinct members are unaffected, which is the assertion that would fail if the bitmask were
        /// keyed on something members share.
        /// </summary>
        [Fact]
        public void DistinctMembersAreUnaffected()
        {
            // a2 6141 0c 6142 0d  -- {"A": 12, "B": 13}
            TwoMemberHolder holder = Cbor.Deserialize<TwoMemberHolder>("A261410C61420D".HexToBytes());

            Assert.Equal(12, holder.A);
            Assert.Equal(13, holder.B);
        }

        public class TwoMemberHolder
        {
            public int A { get; set; }
            public int B { get; set; }
        }

        /// <summary>
        /// A repeated name that matches no member is not a duplicate member. What happens to an unknown
        /// name is <see cref="CborOptions.UnhandledNameMode"/>'s question, and repeating one does not
        /// change the answer.
        /// </summary>
        [Fact]
        public void ARepeatedUnknownNameIsNotReportedAsADuplicate()
        {
            // a3 6141 0c 617a 01 617a 02  -- {"A": 12, "z": 1, "z": 2}
            Holder holder = Cbor.Deserialize<Holder>("A361410C617A01617A02".HexToBytes());

            Assert.Equal(12, holder.A);
        }

        /// <summary>
        /// A member past the 64th is tracked in the fallback rather than the bitmask. Every member is
        /// tried, so the test does not depend on which ordinal a given property happens to be given.
        /// </summary>
        [Fact]
        public void AWideTypeRejectsADuplicateOfAnyMember()
        {
            for (int i = 0; i < 66; i++)
            {
                byte[] document = MapWithRepeatedKey($"P{i}", 1, 2);

                CborException exception = Assert.Throws<CborException>(
                    () => Cbor.Deserialize<WideHolder>(document));

                Assert.Contains("Duplicate map key", exception.Message);
            }
        }

        /// <summary>
        /// And a wide type with no duplicates reads, which is what fails if the fallback reports a
        /// member as seen that was not.
        /// </summary>
        [Fact]
        public void AWideTypeWithoutDuplicatesIsRead()
        {
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(66);

            for (int i = 0; i < 66; i++)
            {
                cborWriter.WriteString($"P{i}");
                cborWriter.WriteInt32(i);
            }

            WideHolder holder = Cbor.Deserialize<WideHolder>(writer.WrittenSpan.ToArray());

            Assert.Equal(0, holder.P0);
            Assert.Equal(65, holder.P65);
        }

        #endregion

        #region LastWins, which has to reach every target or it recreates the problem

        [Fact]
        public void TheObjectModelKeepsTheLastValueUnderLastWins()
        {
            CborObject obj = Cbor.Deserialize<CborObject>(DuplicateLowerA.HexToBytes(), LastWins);

            Assert.Single(obj);
            Assert.Equal(2, obj["a"].Value<int>());
        }

        [Fact]
        public void ADictionaryKeepsTheLastValueUnderLastWins()
        {
            Dictionary<string, int> dictionary =
                Cbor.Deserialize<Dictionary<string, int>>(DuplicateLowerA.HexToBytes(), LastWins);

            Assert.Single(dictionary);
            Assert.Equal(2, dictionary["a"]);
        }

        [Fact]
        public void AConstructedTypeKeepsTheLastValueUnderLastWins()
        {
            ConstructedHolder holder =
                Cbor.Deserialize<ConstructedHolder>(DuplicateId.HexToBytes(), LastWins);

            Assert.Equal(13, holder.Id);
        }

        [Fact]
        public void AMappedClassKeepsTheLastValueUnderLastWins()
        {
            Holder holder = Cbor.Deserialize<Holder>(DuplicateMemberA.HexToBytes(), LastWins);

            Assert.Equal(2, holder.A);
        }

        [Fact]
        public void AStructKeepsTheLastValueUnderLastWins()
        {
            HolderStruct holder = Cbor.Deserialize<HolderStruct>(DuplicateMemberA.HexToBytes(), LastWins);

            Assert.Equal(2, holder.A);
        }

        [Fact]
        public void AnIntKeyedMappedClassKeepsTheLastValueUnderLastWins()
        {
            IndexedHolder holder = Cbor.Deserialize<IndexedHolder>(DuplicateIndexZero.HexToBytes(), LastWins);

            Assert.Equal(13, holder.Id);
        }

        [Fact]
        public void AnIntKeyedConstructedTypeKeepsTheLastValueUnderLastWins()
        {
            IndexedConstructedHolder holder =
                Cbor.Deserialize<IndexedConstructedHolder>(DuplicateIndexZero.HexToBytes(), LastWins);

            Assert.Equal(13, holder.Id);
        }

        [Fact]
        public void AWideTypeKeepsTheLastValueUnderLastWins()
        {
            WideHolder holder = Cbor.Deserialize<WideHolder>(MapWithRepeatedKey("P65", 1, 2), LastWins);

            Assert.Equal(2, holder.P65);
        }

        /// <summary>
        /// A null key is not a duplicate, so <see cref="DuplicateKeyMode.LastWins"/> has nothing to say
        /// about it: there is no earlier occurrence for a later one to win over, and the dictionary
        /// refuses it either way. It must still be reported as what it is.
        /// </summary>
        [Fact]
        public void ANullKeyIsStillRejectedUnderLastWins()
        {
            // a1 f6 01  -- {null: 1}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>("A1F601".HexToBytes(), LastWins));

            Assert.Contains("null", exception.Message);
            Assert.DoesNotContain("Duplicate", exception.Message);
        }

        /// <summary>
        /// A required member supplied twice is still a member that arrived. The two pieces of state are
        /// tracked together, so this is the assertion that fails if the required one stops being
        /// recorded on the mode that does not consult the other.
        /// </summary>
        [Fact]
        public void ARepeatedRequiredMemberIsStillSatisfiedUnderLastWins()
        {
            RequiredHolder holder = Cbor.Deserialize<RequiredHolder>(DuplicateMemberA.HexToBytes(), LastWins);

            Assert.Equal(2, holder.A);
        }

        /// <summary>And a required member that never arrives is still missing, in either mode.</summary>
        [Fact]
        public void AMissingRequiredMemberIsStillReportedUnderLastWins()
        {
            // a0  -- {}
            Assert.Throws<CborException>(() => Cbor.Deserialize<RequiredHolder>("A0".HexToBytes(), LastWins));
        }

        #endregion

        #region The paths where a per-converter ordinal could go wrong

        public abstract class Shape
        {
            public int A { get; set; }
        }

        [CborDiscriminator("Square")]
        public class Square : Shape
        {
            public int B { get; set; }
        }

        private static CborOptions Polymorphic()
        {
            CborOptions options = new CborOptions();
            options.Registry.DiscriminatorConventionRegistry.RegisterType<Square>();
            return options;
        }

        /// <summary>
        /// A polymorphic read runs on the derived type's converter, not the one the call started on,
        /// so the ordinals in play are that converter's. A well-formed document has to survive it.
        /// </summary>
        [Fact]
        public void APolymorphicDocumentWithoutDuplicatesIsRead()
        {
            byte[] document = Map(("_t", "Square"), ("A", 1), ("B", 2));

            Shape shape = Cbor.Deserialize<Shape>(document, Polymorphic());

            Square square = Assert.IsType<Square>(shape);
            Assert.Equal(1, square.A);
            Assert.Equal(2, square.B);
        }

        /// <summary>A member declared on the derived type, repeated.</summary>
        [Fact]
        public void APolymorphicDuplicateOnADerivedMemberIsRejected()
        {
            byte[] document = Map(("_t", "Square"), ("A", 1), ("B", 2), ("B", 3));

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Shape>(document, Polymorphic()));

            Assert.Equal("$.B", exception.Path);
            Assert.Contains("Duplicate map key", exception.Message);
        }

        /// <summary>
        /// And one inherited from the base, which the derived converter numbers among its own members.
        /// </summary>
        [Fact]
        public void APolymorphicDuplicateOnAnInheritedMemberIsRejected()
        {
            byte[] document = Map(("_t", "Square"), ("A", 1), ("A", 2));

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Shape>(document, Polymorphic()));

            Assert.Equal("$.A", exception.Path);
        }

        /// <summary>
        /// The discriminator is a key of the map without being a member of the type, so it has no
        /// ordinal - but it is still a key, and a document repeating it is refused like any other.
        /// Worth refusing specifically: two readers disagreeing on which occurrence names the type is
        /// how a document means one thing here and another thing downstream.
        /// </summary>
        [Fact]
        public void ARepeatedDiscriminatorIsRejected()
        {
            byte[] document = Map(("_t", "Square"), ("_t", "Square"), ("B", 2));

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Shape>(document, Polymorphic()));

            Assert.Contains("Duplicate map key", exception.Message);
            Assert.Contains("_t", exception.Message);
        }

        /// <summary>
        /// And under LastWins it is read like any other repeated key, the last occurrence winning -
        /// which for the discriminator means the first one still chose the type, since that is the one
        /// the convention read on the way in.
        /// </summary>
        [Fact]
        public void ARepeatedDiscriminatorIsAcceptedUnderLastWins()
        {
            CborOptions options = Polymorphic();
            options.DuplicateKeyMode = DuplicateKeyMode.LastWins;

            byte[] document = Map(("_t", "Square"), ("_t", "Square"), ("B", 2));

            Square square = Assert.IsType<Square>(Cbor.Deserialize<Shape>(document, options));
            Assert.Equal(2, square.B);
        }

        /// <summary>
        /// The discriminator is expected where it appears, so it is not an unhandled name. It was
        /// reported as one, which made <see cref="UnhandledNameMode.ThrowException"/> and polymorphism
        /// mutually exclusive: every polymorphic read threw on its own type tag.
        /// </summary>
        [Fact]
        public void TheDiscriminatorIsNotAnUnhandledName()
        {
            CborOptions options = Polymorphic();
            options.UnhandledNameMode = UnhandledNameMode.ThrowException;

            byte[] document = Map(("_t", "Square"), ("A", 1), ("B", 2));

            Square square = Assert.IsType<Square>(Cbor.Deserialize<Shape>(document, options));
            Assert.Equal(1, square.A);
            Assert.Equal(2, square.B);
        }

        /// <summary>
        /// A member name eight bytes long that is the prefix of a longer member's name resolves to no
        /// member, and has to be treated as the unknown name it is rather than as the first member of
        /// the type - which is what the lookup used to answer, ordinal and all.
        /// </summary>
        [Fact]
        public void AKeyThatIsAPrefixOfAMemberNameIsNotAMember()
        {
            byte[] document = Map(("PropertyAlpha", 1), ("Property", 2));

            PrefixHolder holder = Cbor.Deserialize<PrefixHolder>(document);

            Assert.Equal(1, holder.PropertyAlpha);
        }

        public class PrefixHolder
        {
            public int PropertyAlpha { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class ArrayHolder
        {
            [CborProperty(0)]
            public int A { get; set; }

            [CborProperty(1)]
            public int B { get; set; }
        }

        /// <summary>
        /// Array format cannot express a repeated key - members are positional - so nothing there is
        /// ever a duplicate. The assertion worth having is that the tracking does not invent one, since
        /// each item is read at an index the reader advances itself.
        /// </summary>
        [Fact]
        public void ArrayFormatIsUnaffected()
        {
            // 82 0c 0d  -- [12, 13]
            ArrayHolder holder = Cbor.Deserialize<ArrayHolder>("820C0D".HexToBytes());

            Assert.Equal(12, holder.A);
            Assert.Equal(13, holder.B);
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class IndexOneHolder
        {
            [CborProperty(1)]
            public int Value { get; set; }
        }

        /// <summary>
        /// Index 0 is where a discriminator sits, but only for a type that writes one. On a type that
        /// does not, an unmapped 0 is an unknown index like any other and must be reported as one -
        /// recognising it by position alone would swallow it, and report a repeat of it as a duplicate
        /// discriminator the document never carried.
        /// </summary>
        [Fact]
        public void AnUnmappedIndexZeroIsNotTakenForADiscriminator()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            // a2 00 09 01 02  -- {0: 9, 1: 2}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IndexOneHolder>("A2000901 02".Replace(" ", "").HexToBytes(), options));

            Assert.Contains("Unhandled index [0]", exception.Message);
        }

        public class Pair
        {
            public Holder First { get; set; }
            public Holder Second { get; set; }
            public List<Holder> Items { get; set; }
        }

        /// <summary>
        /// Two objects of the same type in one document each get their own state. If it leaked between
        /// them - a field on the converter rather than in the reader context, say - the second would be
        /// refused for a member the first had read.
        /// </summary>
        [Fact]
        public void SiblingsOfTheSameTypeDoNotShareState()
        {
            byte[] document = SiblingsDocument();

            Pair pair = Cbor.Deserialize<Pair>(document);

            Assert.Equal(1, pair.First.A);
            Assert.Equal(2, pair.Second.A);
            Assert.Equal(new[] { 3, 4 }, pair.Items.ConvertAll(item => item.A));
        }

        /// <summary>And a duplicate nested inside one of them is still caught, and named by its path.</summary>
        [Fact]
        public void ADuplicateNestedInAMemberIsRejected()
        {
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(1);
            cborWriter.WriteString("Second");
            cborWriter.WriteBeginMap(2);
            cborWriter.WriteString("A");
            cborWriter.WriteInt32(1);
            cborWriter.WriteString("A");
            cborWriter.WriteInt32(2);

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Pair>(writer.WrittenSpan.ToArray()));

            Assert.Equal("$.Second.A", exception.Path);
        }

        public class MixedHolder
        {
            public int Id { get; set; }
            public int Extra { get; set; }

            [CborConstructor]
            public MixedHolder(int id)
            {
                Id = id;
            }
        }

        /// <summary>
        /// A creator type collects constructor arguments and ordinary members into two different
        /// dictionaries. Both are member reads, so a repeat of either is refused the same way.
        /// </summary>
        [Fact]
        public void AMixedCreatorTypeRejectsADuplicateOnEitherKindOfMember()
        {
            CborException onCreatorMember = Assert.Throws<CborException>(
                () => Cbor.Deserialize<MixedHolder>(Map(("Id", 12), ("Id", 13), ("Extra", 1))));

            Assert.Equal("$.Id", onCreatorMember.Path);

            CborException onRegularMember = Assert.Throws<CborException>(
                () => Cbor.Deserialize<MixedHolder>(Map(("Id", 12), ("Extra", 1), ("Extra", 2))));

            Assert.Equal("$.Extra", onRegularMember.Path);
        }

        [Fact]
        public void AMixedCreatorTypeKeepsTheLastValueOfEitherKindUnderLastWins()
        {
            MixedHolder onCreatorMember =
                Cbor.Deserialize<MixedHolder>(Map(("Id", 12), ("Id", 13), ("Extra", 1)), LastWins);

            Assert.Equal(13, onCreatorMember.Id);

            MixedHolder onRegularMember =
                Cbor.Deserialize<MixedHolder>(Map(("Id", 12), ("Extra", 1), ("Extra", 2)), LastWins);

            Assert.Equal(2, onRegularMember.Extra);
        }

        #endregion

        /// <summary>
        /// A map of text keys to values that are either an int or a string, written in the order given
        /// so that a repeated key stays repeated.
        /// </summary>
        private static byte[] Map(params (string Key, object Value)[] entries)
        {
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(entries.Length);

            foreach ((string key, object value) in entries)
            {
                cborWriter.WriteString(key);

                if (value is int number)
                {
                    cborWriter.WriteInt32(number);
                }
                else
                {
                    cborWriter.WriteString((string)value);
                }
            }

            return writer.WrittenSpan.ToArray();
        }

        /// <summary>{"First": {"A": 1}, "Second": {"A": 2}, "Items": [{"A": 3}, {"A": 4}]}</summary>
        private static byte[] SiblingsDocument()
        {
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(3);

            cborWriter.WriteString("First");
            WriteHolder(ref cborWriter, 1);

            cborWriter.WriteString("Second");
            WriteHolder(ref cborWriter, 2);

            cborWriter.WriteString("Items");
            cborWriter.WriteBeginArray(2);
            WriteHolder(ref cborWriter, 3);
            WriteHolder(ref cborWriter, 4);

            return writer.WrittenSpan.ToArray();
        }

        private static void WriteHolder(ref CborWriter writer, int a)
        {
            writer.WriteBeginMap(1);
            writer.WriteString("A");
            writer.WriteInt32(a);
        }

        /// <summary>
        /// A two-entry map carrying the same text key twice, for keys whose hex is not worth spelling
        /// out by hand.
        /// </summary>
        private static byte[] MapWithRepeatedKey(string key, int first, int second)
        {
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(2);
            cborWriter.WriteString(key);
            cborWriter.WriteInt32(first);
            cborWriter.WriteString(key);
            cborWriter.WriteInt32(second);

            return writer.WrittenSpan.ToArray();
        }
    }
}
