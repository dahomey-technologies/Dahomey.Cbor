using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;
using Dahomey.Cbor.Util;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    /// <summary>
    /// Issue #135: a read failure reported the offending byte but not where in the model it was
    /// reached, leaving the caller to map an offset back onto a POCO graph by hand.
    /// </summary>
    /// <remarks>
    /// The path is assembled as the exception unwinds rather than tracked as the reader descends: the
    /// reader is a <c>ref struct</c> that knows major types, not members, and the hot structural reads
    /// are deliberately inlineable. Each converter names its own position on the way out, so a
    /// successful read allocates nothing for it - it only keeps enough state to answer where it was,
    /// which is an index and, for a map, the current key.
    /// <para>
    /// A path of <c>$</c> means the root value itself was wrong. Every read has at least that much, so
    /// a null <see cref="CborException.Path"/> means the exception did not come from a read at all -
    /// and those two answers are worth telling apart.
    /// </para>
    /// </remarks>
    public class Issue0135
    {
        private class Child
        {
            public string Name { get; set; }
        }

        private class Root
        {
            public Child Child { get; set; }
            public List<Child> Items { get; set; }
            public Child[] Array { get; set; }
            public Dictionary<string, int> Map { get; set; }
            public Dictionary<int, int> IntMap { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyed
        {
            [CborProperty(1)]
            public int Value { get; set; }
        }

        /// <summary>
        /// The failure the issue opened with: the document contradicts the requested type outright.
        /// There is no member to name, but "at the root" is itself the answer.
        /// </summary>
        [Fact]
        public void ARootLevelFailureIsReportedAsTheRoot()
        {
            // 01   1, where a map was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("01".HexToBytes()));

            Assert.Equal("$", exception.Path);
            Assert.Contains("Failed to deserialize from \"$\".", exception.Message);
        }

        [Fact]
        public void AMemberIsNamedByItsPath()
        {
            // a1                  map(1)
            //    64 4e616d65      "Name"
            //    01               1, where a text string was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Name", exception.Path);
        }

        /// <summary>The byte offset the reader already reported is kept, not replaced.</summary>
        [Fact]
        public void ThePathIsAddedToTheExistingMessageRatherThanReplacingIt()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A1644E616D6501".HexToBytes()));

            Assert.StartsWith("[", exception.Message);
            Assert.EndsWith(" Failed to deserialize from \"$.Name\".", exception.Message);
        }

        [Fact]
        public void NestedObjectsCompose()
        {
            // a1                  map(1)
            //    65 4368696c64    "Child"
            //    a1               map(1)
            //       64 4e616d65   "Name"
            //       01            1
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654368696C64A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Child.Name", exception.Path);
        }

        /// <summary>
        /// A member whose value is the wrong shape entirely fails before any of its own members are
        /// reached, so the innermost thing that can be named is the member itself.
        /// </summary>
        [Fact]
        public void AMemberOfTheWrongShapeIsNamedWithoutAnInnerSegment()
        {
            // a1                  map(1)
            //    65 4368696c64    "Child"
            //    01               1, where a map was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654368696C6401".HexToBytes()));

            Assert.Equal("$.Child", exception.Path);
        }

        [Fact]
        public void CollectionItemsAreNamedByIndex()
        {
            // a1                     map(1)
            //    65 4974656d73       "Items"
            //    82                  array(2)
            //       a1 644e616d65 63 616263    {"Name": "abc"}
            //       a1 644e616d65 01           {"Name": 1}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654974656D7382A1644E616D6563616263A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Items[1].Name", exception.Path);
        }

        /// <summary>
        /// The index is the failing item's own, not the count of items read so far, and a root
        /// collection needs no member to hang off.
        /// </summary>
        [Fact]
        public void ARootCollectionNamesTheFailingIndex()
        {
            // 82            array(2)
            //    01         1
            //    63 616263  "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<List<int>>("820163616263".HexToBytes()));

            Assert.Equal("$[1]", exception.Path);
        }

        /// <summary>A failure on the array header is not attributed to its first item.</summary>
        [Fact]
        public void ACollectionOfTheWrongShapeIsNotAttributedToItemZero()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<List<int>>("01".HexToBytes()));

            Assert.Equal("$", exception.Path);
        }

        [Fact]
        public void DictionaryEntriesAreNamedByKey()
        {
            // a1                  map(1)
            //    63 4d6170        "Map"
            //    a2               map(2)
            //       61 61 01      "a": 1
            //       61 62 63616263  "b": "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1634D6170A2616101616263616263".HexToBytes()));

            Assert.Equal("$.Map.b", exception.Path);
        }

        [Fact]
        public void TupleItemsAreNamedByIndex()
        {
            // 82      array(2)
            //    01   1
            //    02   2, where a text string was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<(int, string)>("820102".HexToBytes()));

            Assert.Equal("$[1]", exception.Path);
        }

        /// <summary>
        /// An integer-keyed member is worth naming by the name it has on the type: the index is what
        /// the document said, but the name is what the caller is looking for.
        /// </summary>
        [Fact]
        public void AnIntegerKeyedMemberIsNamedByItsMemberName()
        {
            // a1      map(1)
            //    01   1
            //    63 616263  "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<IntKeyed>("A10163616263".HexToBytes()));

            Assert.Equal("$.Value", exception.Path);
        }

        /// <summary>
        /// A member name comes from the document, so a hostile one is quoted on the same terms as any
        /// other document text rather than copied into the path whole.
        /// </summary>
        [Fact]
        public void ALongMemberNameIsTruncatedInThePath()
        {
            string name = new string('n', 500);
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(1);
            cborWriter.WriteString(name);
            cborWriter.WriteInt32(1);

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(writer.WrittenSpan.ToArray(), options));

            Assert.DoesNotContain(name, exception.Message);
            Assert.True(exception.Message.Length < 300, $"message was {exception.Message.Length} chars");
        }

        /// <summary>
        /// The property stays null for an exception that never went through a read at all.
        /// </summary>
        [Fact]
        public void AnExceptionRaisedOutsideAReadHasNoPath()
        {
            CborException exception = new CborException("boom");

            Assert.Null(exception.Path);
            Assert.Equal("boom", exception.Message);
        }

        /// <summary>
        /// An array member is named the same way a list member is. The two are different converters,
        /// and a path that named the member but not the position would be worse than no path: it would
        /// point at a member that does not exist.
        /// </summary>
        [Fact]
        public void ArrayMembersAreNamedLikeListMembers()
        {
            // a1                     map(1)
            //    65 4172726179       "Array"
            //    82                  array(2)
            //       a1 644e616d65 63 616263    {"Name": "abc"}
            //       a1 644e616d65 01           {"Name": 1}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A165417272617982A1644E616D6563616263A1644E616D6501".HexToBytes()));

            Assert.Equal("$.Array[1].Name", exception.Path);
        }

        [Fact]
        public void ARootArrayNamesTheFailingIndex()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<int[]>("820163616263".HexToBytes()));

            Assert.Equal("$[1]", exception.Path);
        }

        /// <summary>The indefinite-length branch counts positions too, not only the sized one.</summary>
        [Fact]
        public void AnIndefiniteLengthArrayNamesTheFailingIndex()
        {
            // 9f 01 63 616263 ff   array(*) 1 "abc" break
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<int[]>("9F0163616263FF".HexToBytes()));

            Assert.Equal("$[1]", exception.Path);
        }

        /// <summary>
        /// A required member is validated after the read loop rather than inside it, which must not
        /// cost it its path: the same failure reports the same way at the root as when nested.
        /// </summary>
        [Fact]
        public void AMissingRequiredMemberHasAPathAtTheRootToo()
        {
            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Child>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Name).SetRequired(RequirementPolicy.Always)
            );

            // a0   map(0) -- "Name" never arrives
            CborException rootFailure = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A0".HexToBytes(), options));
            CborException nestedFailure = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A1654368696C64A0".HexToBytes(), options));

            Assert.Equal("$", rootFailure.Path);
            Assert.Equal("$.Child", nestedFailure.Path);
        }

        /// <summary>
        /// A scalar root has no structure to describe, but "the root" is still the answer -- and it is
        /// exactly the failure the issue opened with.
        /// </summary>
        [Fact]
        public void AScalarRootFailureIsReportedAsTheRoot()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<int>("63616263".HexToBytes()));

            Assert.Equal("$", exception.Path);
        }

        /// <summary>
        /// A name that is not a plain identifier is bracketed and quoted rather than pasted in, so it
        /// cannot forge path structure or close the quotation the message wraps the path in.
        /// </summary>
        [Fact]
        public void ANameThatWouldBeAmbiguousIsQuoted()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            CborException dotted = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(WriteSingleMemberMap("a.b"), options));
            CborException quoted = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(WriteSingleMemberMap("ev\"il"), options));
            CborException newlined = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(WriteSingleMemberMap("a\nb"), options));

            Assert.Equal("$['a.b']", dotted.Path);
            Assert.Equal("$['ev\\\"il']", quoted.Path);
            Assert.Equal("$['a\\u000ab']", newlined.Path);

            // The delimiter the message puts around the path survives whatever the document sent.
            Assert.EndsWith("\".", quoted.Message);
        }

        /// <summary>
        /// A key that reads as a plain token needs no quoting, whatever the key type is.
        /// </summary>
        [Fact]
        public void AnIntegerDictionaryKeyIsNotQuotedAsAString()
        {
            // a1                     map(1)
            //    66 496e744d6170     "IntMap"
            //    a1 07 63 616263     {7: "abc"}
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Root>("A166496E744D6170A10763616263".HexToBytes()));

            Assert.Equal("$.IntMap.7", exception.Path);
        }

        /// <summary>
        /// The message reads as two sentences whether or not the reader's own message ended in one.
        /// </summary>
        [Fact]
        public void ThePathIsSeparatedFromTheMessageAsASentence()
        {
            CborException noFullStop = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A1644E616D6501".HexToBytes()));

            Assert.Contains(". Failed to deserialize from", noFullStop.Message);
            Assert.DoesNotContain("..", noFullStop.Message);

            CborOptions options = new CborOptions();
            options.Registry.ObjectMappingRegistry.Register<Child>(objectMapping =>
                objectMapping
                    .AutoMap()
                    .ClearMemberMappings()
                    .MapMember(o => o.Name).SetRequired(RequirementPolicy.DisallowNull)
            );

            // a1 644e616d65 f6  -- {"Name": null}, whose message already ends in a full stop
            CborException fullStop = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>("A1644E616D65F6".HexToBytes(), options));

            Assert.Equal("Property 'Name' cannot be null. Failed to deserialize from \"$.Name\".", fullStop.Message);
        }

        /// <summary>
        /// The object model reaches the same converters by a different door, so it has to mark the
        /// root as well - otherwise a scalar conversion is exactly the case that reports nothing,
        /// which is what <see cref="CborException.Path"/> now promises cannot happen.
        /// </summary>
        [Fact]
        public void ConvertingFromTheObjectModelReportsTheRootToo()
        {
            CborObject source = new CborObject { ["Name"] = "abc" };

            CborException scalar = Assert.Throws<CborException>(() => source.ToObject<int>());
            CborException collection = Assert.Throws<CborException>(() => source.ToObject<List<int>>());
            CborException array = Assert.Throws<CborException>(
                () => new CborArray(1, "abc").ToCollection<List<int>>());

            Assert.Equal("$", scalar.Path);
            Assert.Equal("$", collection.Path);
            Assert.Equal("$[1]", array.Path);
        }

        /// <summary>
        /// The budget is spent on the rendered result, not on the source. Counting the source would
        /// let a name of control characters - each of which renders as six - past a bound that looks
        /// like 64 and is really 384.
        /// </summary>
        [Fact]
        public void TheLengthBoundHoldsAgainstTextChosenToDefeatIt()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(WriteSingleMemberMap(new string('\n', 300)), options));

            Assert.True(exception.Message.Length < 300, $"message was {exception.Message.Length} chars");
            Assert.Contains("(300 characters)", exception.Path);
        }

        /// <summary>
        /// Truncation lands between characters, never inside one: half a surrogate pair is not valid
        /// UTF-16 and can break whatever the message is written to.
        /// </summary>
        [Fact]
        public void TruncationDoesNotSplitASurrogatePair()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };
            string name = new string('a', 63) + "\U0001F600" + new string('a', 40);

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(WriteSingleMemberMap(name), options));

            string path = Assert.IsType<string>(exception.Path);

            for (int i = 0; i < path.Length; i++)
            {
                if (char.IsHighSurrogate(path[i]))
                {
                    Assert.True(i + 1 < path.Length && char.IsLowSurrogate(path[i + 1]), "high surrogate left unpaired");
                }
                else
                {
                    Assert.False(char.IsLowSurrogate(path[i]), "low surrogate left unpaired");
                }
            }
        }

        /// <summary>
        /// A non-ASCII member name survives the round trip into the message it is rejected with.
        /// </summary>
        [Fact]
        public void AnUnhandledNameIsDecodedAsUtf8()
        {
            CborOptions options = new CborOptions { UnhandledNameMode = UnhandledNameMode.ThrowException };

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Child>(WriteSingleMemberMap("naïve"), options));

            Assert.Contains("naïve", exception.Message);
            Assert.Equal("$.naïve", exception.Path);
        }

        /// <summary>
        /// A converter that decodes a structure of its own can name positions inside it, which is
        /// otherwise the one thing the framework cannot do on a caller's behalf.
        /// </summary>
        private class BodyConverter : CborConverterBase<Body>
        {
            public override Body Read(ref CborReader reader)
            {
                reader.ReadBeginArray();
                reader.ReadSize();

                try
                {
                    return new Body { Count = reader.ReadInt32() };
                }
                catch (CborException exception)
                {
                    exception.PrependPathIndex(0);
                    throw;
                }
            }

            public override void Write(ref CborWriter writer, Body value, LengthMode lengthMode)
            {
                writer.WriteBeginArray(1);
                writer.WriteInt32(value.Count);
                writer.WriteEndArray(1);
            }
        }

        private class Body
        {
            public int Count { get; set; }
        }

        private class BodyHolder
        {
            public Body Body { get; set; }
        }

        [Fact]
        public void ACustomConverterCanNamePositionsWithinItself()
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverter(typeof(Body), new BodyConverter());

            // a1                  map(1)
            //    64 426f6479      "Body"
            //    81               array(1)
            //       63 616263     "abc", where an integer was required
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<BodyHolder>("A164426F64798163616263".HexToBytes(), options));

            Assert.Equal("$.Body[0]", exception.Path);
        }

        /// <summary>
        /// Doing nothing remains correct: a custom converter that adds no segment is still named by
        /// the member holding it, which is why the seam above is a refinement rather than a duty.
        /// </summary>
        [Fact]
        public void ACustomConverterThatAddsNothingIsStillNamedByItsMember()
        {
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverter(typeof(Body), new SilentBodyConverter());

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<BodyHolder>("A164426F64798163616263".HexToBytes(), options));

            Assert.Equal("$.Body", exception.Path);
        }

        private class SilentBodyConverter : CborConverterBase<Body>
        {
            public override Body Read(ref CborReader reader)
            {
                reader.ReadBeginArray();
                reader.ReadSize();
                return new Body { Count = reader.ReadInt32() };
            }

            public override void Write(ref CborWriter writer, Body value, LengthMode lengthMode)
            {
                writer.WriteBeginArray(1);
                writer.WriteInt32(value.Count);
                writer.WriteEndArray(1);
            }
        }

        private static byte[] WriteSingleMemberMap(string memberName)
        {
            ByteBufferWriter writer = new ByteBufferWriter();
            CborWriter cborWriter = new CborWriter(writer);
            cborWriter.WriteBeginMap(1);
            cborWriter.WriteString(memberName);
            cborWriter.WriteInt32(1);

            return writer.WrittenSpan.ToArray();
        }
    }
}
