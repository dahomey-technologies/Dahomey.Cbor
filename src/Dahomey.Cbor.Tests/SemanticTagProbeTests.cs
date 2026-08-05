using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A semantic tag in front of a value must survive the read path's probes and reach the
    /// converter that owns it.
    /// </summary>
    /// <remarks>
    /// Every <see cref="CborReader"/> read entry point begins with <c>SkipSemanticTag()</c>, so for
    /// converters that ignore tags a probe consuming one is invisible: the value still decodes. It
    /// stops being invisible as soon as a converter needs to <em>see</em> its tag, which is what
    /// <see cref="TagObservingConverter"/> does here — it is the smallest thing that can tell the
    /// difference, and it stands in for any converter decoded from its tag.
    /// <para>
    /// Four probes consumed a tag while only meaning to look at one: the null check in
    /// <c>MemberConverter</c> and <c>StructMemberConverter</c>, the discriminator test in
    /// <c>ObjectConverter</c>'s Array format, the break checks in <c>MoveNextMapItem</c>,
    /// <c>ReadArray</c>, <c>SkipArray</c> and <c>SkipMap</c>, and the same break check across the
    /// tuple converters.
    /// </para>
    /// </remarks>
    public class SemanticTagProbeTests
    {
        [CborConverter(typeof(TagObservingConverter))]
        public class Tagged
        {
            public ulong Tag { get; set; }
            public int Value { get; set; }
        }

        /// <summary>
        /// Reads its own semantic tag instead of letting the reader skip it, so a probe that
        /// consumed the tag shows up as <c>Tag == 0</c> rather than as a decoding failure.
        /// </summary>
        public class TagObservingConverter : CborConverterBase<Tagged>
        {
            public override Tagged Read(ref CborReader reader)
            {
                ulong tag = reader.TryReadSemanticTag(out ulong semanticTag) ? semanticTag : 0;

                return new Tagged { Tag = tag, Value = reader.ReadInt32() };
            }

            public override void Write(ref CborWriter writer, Tagged value, LengthMode lengthMode)
            {
                writer.WriteSemanticTag(value.Tag);
                writer.WriteInt32(value.Value);
            }
        }

        public class Holder
        {
            public Tagged Member { get; set; }
        }

        public struct StructHolder
        {
            public Tagged Member { get; set; }
        }

        /// <summary>
        /// A string member, so the null case is about the probe rather than about whether the
        /// member's converter happens to handle null.
        /// </summary>
        public class RequiredHolder
        {
            [CborRequired(RequirementPolicy.DisallowNull)]
            public string Member { get; set; }
        }

        /// <summary>
        /// The Array object format keys members by position, so every member needs an explicit index.
        /// </summary>
        public class ArrayHolder
        {
            [CborProperty(0)]
            public Tagged Member { get; set; }
        }

        public class IntHolder
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// The null probe on a class member. This is every object member in the library, so before
        /// the fix no tag-decoded value could be read back as a member of anything.
        /// </summary>
        [Fact]
        public void AClassMemberKeepsItsSemanticTag()
        {
            // a1                     map(1)
            //    66 4d656d626572     "Member"
            //    c1                  tag(1)
            //    05                  5
            Holder holder = Helper.Read<Holder>("A1664D656D626572C105");

            Assert.Equal(1UL, holder.Member.Tag);
            Assert.Equal(5, holder.Member.Value);
        }

        /// <summary>Same probe, struct member path.</summary>
        [Fact]
        public void AStructMemberKeepsItsSemanticTag()
        {
            StructHolder holder = Helper.Read<StructHolder>("A1664D656D626572C105");

            Assert.Equal(1UL, holder.Member.Tag);
            Assert.Equal(5, holder.Member.Value);
        }

        /// <summary>
        /// The break probe in <c>ReadArray</c>. A break marker is never tagged, so a tag here belongs
        /// to the next item and must survive.
        /// </summary>
        [Fact]
        public void AnIndefiniteLengthArrayKeepsItemTags()
        {
            // 9f            array(*)
            //    c1 05      tag(1) 5
            //    c2 06      tag(2) 6
            // ff            break
            List<Tagged> items = Cbor.Deserialize<List<Tagged>>("9FC105C206FF".HexToBytes());

            Assert.Equal(2, items.Count);
            Assert.Equal(1UL, items[0].Tag);
            Assert.Equal(5, items[0].Value);
            Assert.Equal(2UL, items[1].Tag);
            Assert.Equal(6, items[1].Value);
        }

        /// <summary>The break probe in <c>MoveNextMapItem</c>, on an indefinite-length map.</summary>
        [Fact]
        public void AnIndefiniteLengthMapKeepsValueTags()
        {
            // bf                     map(*)
            //    66 4d656d626572     "Member"
            //    c1 05               tag(1) 5
            // ff                     break
            Holder holder = Helper.Read<Holder>("BF664D656D626572C105FF");

            Assert.Equal(1UL, holder.Member.Tag);
            Assert.Equal(5, holder.Member.Value);
        }

        /// <summary>
        /// A definite-length array of tagged items, so the fix is not only about the break probe.
        /// </summary>
        [Fact]
        public void ADefiniteLengthArrayKeepsItemTags()
        {
            List<Tagged> items = Cbor.Deserialize<List<Tagged>>("82C105C206".HexToBytes());

            Assert.Equal(2, items.Count);
            Assert.Equal(1UL, items[0].Tag);
            Assert.Equal(2UL, items[1].Tag);
        }

        /// <summary>
        /// <c>ObjectConverter</c>'s Array format tested for the discriminator tag and discarded
        /// whatever it found. A tag that is not the discriminator tag belongs to the first item.
        /// </summary>
        [Fact]
        public void ArrayFormatKeepsATagOnTheFirstItemWhenItIsNotTheDiscriminator()
        {
            CborOptions options = new CborOptions { ObjectFormat = CborObjectFormat.Array };

            // 81            array(1)
            //    c1 05      tag(1) 5      <- tag 1, not the discriminator tag (39)
            ArrayHolder holder = Helper.Read<ArrayHolder>("81C105", options);

            Assert.Equal(1UL, holder.Member.Tag);
            Assert.Equal(5, holder.Member.Value);
        }

        public class TupleHolder
        {
            public (int, int) T { get; set; }
        }

        /// <summary>
        /// A tagged tuple must stay readable. This one guards against a regression introduced by
        /// this change rather than a bug on master.
        /// </summary>
        /// <remarks>
        /// <c>TupleConverter</c> is the only converter that reaches the reader below its
        /// tag-skipping entry points -- <c>Read</c> opens with <c>ReadSize()</c>, which inspects the
        /// header directly -- so it relied on the member null probe consuming the tag on its behalf.
        /// Once that probe stopped consuming, the tag byte's low five bits were read as the array
        /// size and the arity check fired. Each arity now skips its own tag.
        /// </remarks>
        [Fact]
        public void ATaggedTupleIsStillReadable()
        {
            // a1              map(1)
            //    61 54        "T"
            //    c1           tag(1)
            //    82 01 02     [1, 2]
            TupleHolder holder = Helper.Read<TupleHolder>("A16154C1820102");

            Assert.Equal((1, 2), holder.T);
        }

        /// <summary>
        /// The tuple converters ran the same break probe once per arity.
        /// </summary>
        [Fact]
        public void ATupleKeepsItemTags()
        {
            (Tagged, Tagged) pair = Cbor.Deserialize<(Tagged, Tagged)>("82C105C206".HexToBytes());

            Assert.Equal(1UL, pair.Item1.Tag);
            Assert.Equal(2UL, pair.Item2.Tag);
        }

        /// <summary>
        /// Writing is unchanged: the tag is emitted by the converter exactly as before, and the
        /// probes were only ever on the read side.
        /// </summary>
        [Fact]
        public void WritingIsUnaffected()
        {
            Holder holder = new Holder { Member = new Tagged { Tag = 1, Value = 5 } };

            Helper.TestWrite(holder, "A1664D656D626572C105");
        }

        /// <summary>
        /// The one behaviour that does change, pinned deliberately.
        /// </summary>
        /// <remarks>
        /// The null probe inspects the header rather than skipping tags, so a null behind a semantic
        /// tag no longer reads as null to the probe. The value is unaffected — the member's converter
        /// calls <c>ReadNull()</c>, which skips the tag and yields null — but
        /// <see cref="RequirementPolicy.DisallowNull"/> no longer rejects this one shape. The
        /// alternative is a bookmark copy on every member of every object read, which is a cost paid
        /// by everyone to reject a value almost nobody writes.
        /// </remarks>
        [Fact]
        public void ATaggedNullIsNotRejectedByDisallowNull()
        {
            // a1                     map(1)
            //    66 4d656d626572     "Member"
            //    c1                  tag(1)
            //    f6                  null
            // a1                     map(1)
            //    66 4d656d626572     "Member"
            //    c1                  tag(1)
            //    f6                  null
            RequiredHolder holder = Cbor.Deserialize<RequiredHolder>(
                "A1664D656D626572C1F6".HexToBytes());

            Assert.Null(holder.Member);
        }

        /// <summary>An untagged null is still rejected, which is the case that matters.</summary>
        [Fact]
        public void AnUntaggedNullIsStillRejectedByDisallowNull()
        {
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<RequiredHolder>("A1664D656D626572F6".HexToBytes()));
        }

        /// <summary>
        /// Nested semantic tags do not work, on this branch or on master, and this change neither
        /// fixes nor worsens them.
        /// </summary>
        /// <remarks>
        /// <c>GetCurrentDataItemType</c>'s <c>case SemanticTag: Advance(1); return
        /// GetCurrentDataItemType();</c> advances a second time after <c>GetHeader()</c> has already
        /// consumed the header byte, so the recursion skips the tag byte and the byte after it. The
        /// case is pinned here so that fixing it later is a deliberate change to a stated
        /// expectation rather than a silent one.
        /// </remarks>
        [Fact]
        public void NestedSemanticTagsAreNotSupported()
        {
            // a1                  map(1)
            //    65 56616c7565    "Value"
            //    c1 c0            tag(1) tag(0)
            //    01               1
            Assert.Throws<CborException>(
                () => Cbor.Deserialize<IntHolder>("A16556616C7565C1C001".HexToBytes()));
        }
    }
}
