using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Dahomey.Cbor.Util;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A CBOR map carrying the same key twice is rejected, and the rejection is a
    /// <see cref="CborException"/> like every other malformed-input failure.
    /// </summary>
    /// <remarks>
    /// The document was already refused — the backing dictionary's own <c>Add</c> threw — but as an
    /// <see cref="System.ArgumentException"/>, which a caller's <c>catch (CborException)</c> does not
    /// catch. For anything decoding untrusted frames that is the difference between a handled bad frame
    /// and an unhandled exception, and nothing in the message said which key or where.
    /// <para>
    /// Rejecting rather than letting the last occurrence win is the behaviour that was already there;
    /// this changes the exception, not the outcome. Silently keeping one of two values for the same key
    /// would be a data-integrity decision, and a much larger change than a contract fix.
    /// </para>
    /// </remarks>
    public class DuplicateMapKeyTests
    {
        /// <summary>The object model, reached through <c>CborValueConverter</c>.</summary>
        [Fact]
        public void ADuplicateKeyInTheObjectModelThrowsCborException()
        {
            // a2 6161 01 6161 02  -- {"a": 1, "a": 2}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<CborObject>("A2616101616102".HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        /// <summary>
        /// A typed dictionary, which reaches the same shape through a different converter.
        /// </summary>
        [Fact]
        public void ADuplicateKeyInADictionaryThrowsCborException()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>("A2616101616102".HexToBytes()));

            Assert.Contains("Duplicate map key", exception.Message);
        }

        /// <summary>
        /// The message names the key, since the whole point of the change is a caller being able to say
        /// what was wrong with the frame it rejected.
        /// </summary>
        [Fact]
        public void TheMessageNamesTheDuplicatedKey()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>("A2616101616102".HexToBytes()));

            Assert.Contains("a", exception.Message);
        }

        /// <summary>An integer key, so the message is not string-specific.</summary>
        [Fact]
        public void ADuplicateIntegerKeyIsAlsoRejected()
        {
            // a2 01 01 01 02  -- {1: 1, 1: 2}
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<int, int>>("A201010102".HexToBytes()));
        }

        /// <summary>
        /// The duplicate may be nested rather than at the root, and must still be caught rather than
        /// escaping as something else from inside a converter.
        /// </summary>
        [Fact]
        public void ADuplicateKeyNestedInsideAMapIsRejected()
        {
            // a1 6161 a2 6162 01 6162 02  -- {"a": {"b": 1, "b": 2}}
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<CborObject>("A16161A2616201616202".HexToBytes()));
        }

        /// <summary>
        /// Distinct keys are unaffected, including two that only differ by type — an integer 1 and the
        /// text "1" are different keys and both must survive.
        /// </summary>
        [Fact]
        public void DistinctKeysAreUnaffected()
        {
            // a2 01 01 6131 02  -- {1: 1, "1": 2}
            CborObject obj = Cbor.Deserialize<CborObject>("A20101613102".HexToBytes());

            Assert.Equal(2, obj.Count);
        }

        /// <summary>
        /// Duplicate members of a POCO are a different question and deliberately unchanged: the last
        /// occurrence wins, as it did before, because a member is matched by name rather than inserted
        /// into a dictionary.
        /// </summary>
        [Fact]
        public void DuplicatePocoMembersStillTakeTheLastValue()
        {
            // a2 6141 01 6141 02  -- {"A": 1, "A": 2}
            DuplicateHolder holder = Cbor.Deserialize<DuplicateHolder>("A2614101614102".HexToBytes());

            Assert.Equal(2, holder.A);
        }

        public class DuplicateHolder
        {
            public int A { get; set; }
        }

        /// <summary>
        /// A type with a creator mapping takes a different branch of <c>ObjectConverter</c>: member
        /// values are collected into dictionaries until the constructor can be called, rather than
        /// being set on an instance. Those dictionaries reject a duplicate too, so the same contract
        /// applies — a record or a <c>[CborConstructor]</c> type is an ordinary decode target.
        /// </summary>
        [Fact]
        public void ADuplicateMemberOfAConstructedTypeThrowsCborException()
        {
            // a2 624964 0c 624964 0d  -- {"Id": 12, "Id": 13}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<ConstructedHolder>("A26249640C6249640D".HexToBytes()));

            Assert.Contains("Duplicate", exception.Message);
        }

        /// <summary>The same type in <c>IntKeyMap</c> format, which collects by index instead.</summary>
        [Fact]
        public void ADuplicateIndexedMemberOfAConstructedTypeThrowsCborException()
        {
            // a2 00 0c 00 0d  -- {0: 12, 0: 13}
            CborOptions options = new CborOptions { ObjectFormat = CborObjectFormat.IntKeyMap };

            Assert.Throws<CborException>(
                () => Cbor.Deserialize<IndexedConstructedHolder>("A2000C000D".HexToBytes(), options));
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

        /// <summary>
        /// A null key is not a duplicate, and must not be reported as one. <c>ArgumentNullException</c>
        /// derives from <see cref="System.ArgumentException"/>, so a catch that does not distinguish
        /// them turns "the key was null" into "duplicate map key: " with nothing after the colon.
        /// </summary>
        [Fact]
        public void ANullKeyIsReportedAsANullKeyRatherThanADuplicate()
        {
            // a1 f6 01  -- {null: 1}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>("A1F601".HexToBytes()));

            Assert.Contains("null", exception.Message);
            Assert.DoesNotContain("Duplicate", exception.Message);
        }

        /// <summary>
        /// A key from an untrusted document is not repeated at length into the exception message, which
        /// typically reaches a log. The message says what was wrong without carrying a megabyte of
        /// attacker-chosen text with it.
        /// </summary>
        [Fact]
        public void ALongDuplicateKeyIsTruncatedInTheMessage()
        {
            string key = new string('k', 500);
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(2);
            cborWriter.WriteString(key);
            cborWriter.WriteInt32(1);
            cborWriter.WriteString(key);
            cborWriter.WriteInt32(2);

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>(writer.WrittenSpan.ToArray()));

            Assert.True(exception.Message.Length < 200, $"message was {exception.Message.Length} chars");
            Assert.Contains("Duplicate map key", exception.Message);
        }
    }
}
