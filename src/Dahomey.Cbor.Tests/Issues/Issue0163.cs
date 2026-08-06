using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #163: an <c>undefined</c> (<c>F7</c>) read into a <see cref="CborValue"/> was reported as
    /// read without being consumed, so the rest of its container was decoded from a stale header.
    /// </summary>
    /// <remarks>
    /// <c>GetCurrentDataItemType</c> maps both <c>Null</c> and <c>Undefined</c> to
    /// <see cref="CborDataItemType.Null"/>, but <c>ReadNull()</c> accepts only <c>F6</c>: for <c>F7</c>
    /// it returns false having advanced nothing, while the header byte has already been read into the
    /// reader's cache. <c>CborValueConverter</c> returned <see cref="CborValue.Null"/> regardless, so
    /// the next read was handed that same cached <c>F7</c> header back.
    /// <para>
    /// It is invisible when the <c>undefined</c> is its container's last item, which is why the
    /// existing coverage did not catch it — the container's item count runs out before anything looks
    /// again. <see cref="AnUndefinedAsTheLastItemWasNeverAffected"/> pins that case so a future change
    /// cannot quietly break the shape that always worked.
    /// </para>
    /// </remarks>
    public class Issue0163
    {
        public class Holder
        {
            public CborValue A { get; set; }
            public int B { get; set; }
        }

        /// <summary>
        /// The silent one: no exception, and a document that decodes to something else entirely.
        /// </summary>
        [Fact]
        public void AnUndefinedInAMapDoesNotSwallowTheFollowingPair()
        {
            // a2                  map(2)
            //    61 41            "A"
            //    f7               undefined
            //    61 42            "B"
            //    01               1
            CborObject obj = Cbor.Deserialize<CborObject>("A26141F7614201".HexToBytes());

            Assert.Equal(2, obj.Count);
            Assert.Equal(CborValueType.Null, obj["A"].Type);
            Assert.Equal(1, obj["B"].Value<int>());
        }

        /// <summary>
        /// The same document into a POCO, where the stale header reached <c>ReadRawString</c> and threw
        /// about the member <em>after</em> the undefined one.
        /// </summary>
        [Fact]
        public void AnUndefinedMemberDoesNotBreakTheFollowingMember()
        {
            Holder holder = Cbor.Deserialize<Holder>("A26141F7614201".HexToBytes());

            Assert.Equal(CborValueType.Null, holder.A.Type);
            Assert.Equal(1, holder.B);
        }

        /// <summary>
        /// The indefinite-length form failed differently again: the stale header was re-read as another
        /// null key, and the duplicate collided in the backing dictionary as an
        /// <see cref="ArgumentException"/> — outside the <see cref="CborException"/> contract a caller
        /// wraps deserialization in.
        /// </summary>
        [Fact]
        public void AnUndefinedInAnIndefiniteLengthMapDoesNotCollideOnANullKey()
        {
            // bf                  map(*)
            //    61 41 f7         "A": undefined
            //    61 42 01         "B": 1
            // ff                  break
            CborObject obj = Cbor.Deserialize<CborObject>("BF6141F7614201FF".HexToBytes());

            Assert.Equal(2, obj.Count);
            Assert.Equal(CborValueType.Null, obj["A"].Type);
            Assert.Equal(1, obj["B"].Value<int>());
        }

        /// <summary>An array item, which reaches the same converter by a different route.</summary>
        [Fact]
        public void AnUndefinedInAnArrayDoesNotSwallowTheFollowingItem()
        {
            // 82 f7 01            [undefined, 1]
            CborArray array = Cbor.Deserialize<CborArray>("82F701".HexToBytes());

            Assert.Equal(2, array.Count);
            Assert.Equal(CborValueType.Null, array[0].Type);
            Assert.Equal(1, array[1].Value<int>());
        }

        /// <summary>
        /// The shape that always worked, and still reads as null rather than gaining a distinct
        /// <c>undefined</c> value: <c>CborValueType.Undefined</c> stays the default-struct sentinel it
        /// has always been, since no concrete <see cref="CborValue"/> reports it.
        /// </summary>
        [Fact]
        public void AnUndefinedAsTheLastItemWasNeverAffected()
        {
            // a2 6142 01 6141 f7  -- {"B": 1, "A": undefined}
            Holder holder = Cbor.Deserialize<Holder>("A26142016141F7".HexToBytes());

            Assert.Equal(CborValueType.Null, holder.A.Type);
            Assert.Equal(1, holder.B);
        }

        /// <summary>
        /// A tagged <c>undefined</c> takes the same path, since <c>ReadNull</c> skips the tag before
        /// failing to accept the primitive behind it.
        /// </summary>
        [Fact]
        public void ATaggedUndefinedIsAlsoConsumed()
        {
            // a2 6141 c1f7 6142 01  -- {"A": tag(1) undefined, "B": 1}
            CborObject obj = Cbor.Deserialize<CborObject>("A26141C1F7614201".HexToBytes());

            Assert.Equal(2, obj.Count);
            Assert.Equal(CborValueType.Null, obj["A"].Type);
            Assert.Equal(1, obj["B"].Value<int>());
        }
    }
}
