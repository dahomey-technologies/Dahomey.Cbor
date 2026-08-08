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
