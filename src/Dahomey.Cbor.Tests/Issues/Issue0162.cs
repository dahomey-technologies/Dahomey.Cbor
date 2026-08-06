using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #162, first half: a semantic tag captured on read must not attach itself to every other
    /// occurrence of that value in the process.
    /// </summary>
    /// <remarks>
    /// <see cref="CborPositive"/> and <see cref="CborNegative"/> hand out shared instances for small
    /// integers, as <see cref="CborSingle"/>, <see cref="CborDouble"/> and <see cref="CborDecimal"/> do
    /// for small whole numbers, <see cref="CborBoolean"/> for both values and
    /// <see cref="CborValue.Null"/> for null. Assigning <c>SemanticTag</c> to one of those attaches the
    /// tag to that value everywhere — in documents that never carried a tag, and in values built in
    /// code that were never read at all.
    /// <para>
    /// This is observable today through the property, and it is what would turn emitting tags on write
    /// into wrong bytes rather than a fix.
    /// </para>
    /// </remarks>
    public class Issue0162
    {
        [Fact]
        public void ATagOnOneDocumentDoesNotLeakIntoAnother()
        {
            CborValue tagged = Cbor.Deserialize<CborValue>("C101".HexToBytes());
            Assert.Equal(1UL, tagged.SemanticTag);

            // A different document, carrying no tag, whose items are the same integers.
            CborArray other = Cbor.Deserialize<CborArray>("820102".HexToBytes());

            Assert.Null(other[0].SemanticTag);
            Assert.Null(other[1].SemanticTag);
        }

        /// <summary>The same for the shared null, which is one instance for the whole process.</summary>
        [Fact]
        public void ATagOnANullDoesNotLeakIntoTheSharedNull()
        {
            CborValue tagged = Cbor.Deserialize<CborValue>("C2F6".HexToBytes());

            Assert.Equal(2UL, tagged.SemanticTag);
            Assert.Null(CborValue.Null.SemanticTag);
        }

        /// <summary>
        /// A value built in code, never read from anything, must not acquire a tag either — the shared
        /// instance a literal resolves to is the same one a document would have tagged.
        /// </summary>
        [Fact]
        public void AValueBuiltInCodeNeverAcquiresATag()
        {
            Cbor.Deserialize<CborValue>("C101".HexToBytes());

            CborArray built = new CborArray { 1, 2 };

            Assert.Null(built[0].SemanticTag);
        }
    }
}
