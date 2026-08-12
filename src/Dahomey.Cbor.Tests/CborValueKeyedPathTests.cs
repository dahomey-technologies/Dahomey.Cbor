using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// How a dictionary keyed by <see cref="CborValue"/> names its keys in
    /// <see cref="CborException.Path"/>.
    /// </summary>
    /// <remarks>
    /// This is the one decode target that names a key twice in a single exception:
    /// <c>AbstractDictionaryConverter</c> describes it once for the message and again for the path
    /// segment. #191 unwrapped a <see cref="CborString"/> for the message only, so the two disagreed -
    /// <c>Duplicate map key: a</c> reported at <c>$['\"a\"']</c>, which is #178's defect inside one
    /// message. Both now name the key through <c>MapKeyErrors.KeyText</c>.
    /// <para>
    /// The path is the wider half of that fix: it is built for every failure under such a dictionary,
    /// not only for a duplicate, so most of what these tests pin has nothing to do with duplicate keys.
    /// </para>
    /// </remarks>
    public class CborValueKeyedPathTests
    {
        /// <summary>
        /// The duplicate that motivated the change: message and path name the key the same way.
        /// </summary>
        [Fact]
        public void ThePathNamesTheKeyTheSameWayTheMessageDoes()
        {
            // a2 6161 01 6161 02  -- {"a": 1, "a": 2}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<CborValue, int>>("A2616101616102".HexToBytes()));

            Assert.Contains("Duplicate map key: a", exception.Message);
            Assert.Equal("$.a", exception.Path);
        }

        /// <summary>
        /// A failure that is not a duplicate is pathed the same way, since the path segment is built
        /// for any failure under the entry rather than for duplicates specifically.
        /// </summary>
        [Fact]
        public void AValueThatFailsToReadIsPathedByTheKeyText()
        {
            // a1 6161 6178  -- {"a": "x"}, into a dictionary whose values are ints
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<CborValue, int>>("A161616178".HexToBytes()));

            Assert.Equal("$.a", exception.Path);
        }

        /// <summary>
        /// The same document read into a <c>string</c>-keyed dictionary reports the same path, which is
        /// the property the change is for: the key names the position, not the type it was read into.
        /// </summary>
        [Fact]
        public void ACborValueKeyAndAStringKeyAgreeOnThePath()
        {
            const string hexBuffer = "A161616178"; // a1 6161 6178  -- {"a": "x"}

            string? cborValueKeyed = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<CborValue, int>>(hexBuffer.HexToBytes())).Path;
            string? stringKeyed = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<string, int>>(hexBuffer.HexToBytes())).Path;

            Assert.Equal(stringKeyed, cborValueKeyed);
        }

        /// <summary>
        /// Segments compose, so the quoting was previously paid once per level. This is the case the
        /// change improves most: two levels deep read <c>$['\"x\"']['\"a\"']</c> before it.
        /// </summary>
        [Fact]
        public void NestedCborValueKeysComposeIntoAPlainPath()
        {
            // a1 6178 a1 6161 6179  -- {"x": {"a": "y"}}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<CborValue, Dictionary<CborValue, int>>>(
                    "A16178A161616179".HexToBytes()));

            Assert.Equal("$.x.a", exception.Path);
        }

        /// <summary>
        /// An empty text key is bracketed rather than rendered as nothing, so it stays distinguishable
        /// from a key that could not be named at all - which is reported by index instead.
        /// </summary>
        [Fact]
        public void AnEmptyTextKeyIsPathedAsTheEmptyString()
        {
            // a1 60 6178  -- {"": "x"}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<CborValue, int>>("A1606178".HexToBytes()));

            Assert.Equal("$['']", exception.Path);
        }

        /// <summary>
        /// A key with no quoting of its own is still pathed by its own <c>ToString</c>: only the string
        /// case is unwrapped.
        /// </summary>
        /// <remarks>
        /// The cost of unwrapping, and the reason this is pinned: a text key <c>"[1]"</c> now paths
        /// identically to the array key <c>[1]</c> asserted here, where before the quoting told them
        /// apart. That is the same trade #191 accepted for the message, where the text key <c>"1"</c>
        /// and the integer key <c>1</c> both render <c>1</c> - taken here so the path agrees with the
        /// message rather than being independently unambiguous.
        /// </remarks>
        [Fact]
        public void ANonTextKeyIsPathedByItsOwnToString()
        {
            // a1 8101 6178  -- {[1]: "x"}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Dictionary<CborValue, int>>("A181016178".HexToBytes()));

            Assert.Equal("$['[1]']", exception.Path);
        }
    }
}
