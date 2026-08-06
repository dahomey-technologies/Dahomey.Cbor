using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
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
    }
}
