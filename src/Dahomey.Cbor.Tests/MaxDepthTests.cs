#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Covers <see cref="CborOptions.MaxDepth"/>.
    /// </summary>
    /// <remarks>
    /// Two distinct hazards, one mechanism. On write, a reference cycle is not representable in CBOR
    /// (there are no back-references) and recurses forever. On read, a handful of bytes can describe
    /// arbitrarily deep nesting, so hostile input can exhaust the stack. Both used to be
    /// <c>StackOverflowException</c>, which cannot be caught and takes the process down — meaning
    /// neither could even be covered by a test. Bounding depth turns both into
    /// <see cref="CborException"/>.
    /// </remarks>
    public class MaxDepthTests
    {
        public class Node
        {
            public Node? Next { get; set; }
        }

        // ---- the payoff: a reference cycle is now catchable ----

        [Fact]
        public void ReferenceCycleThrowsInsteadOfOverflowingTheStack()
        {
            Node node = new Node();
            node.Next = node;

            CborException exception = Assert.Throws<CborException>(() => Helper.Write(node));

            Assert.Contains("nesting depth", exception.Message);
            Assert.Contains("reference cycle", exception.Message);
        }

        [Fact]
        public void LongerReferenceCycleAlsoThrows()
        {
            Node first = new Node();
            Node second = new Node { Next = first };
            first.Next = second;

            Assert.Throws<CborException>(() => Helper.Write(first));
        }

        [Fact]
        public void SelfReferentialTypeWithoutACycleStillWorks()
        {
            Node chain = new Node { Next = new Node { Next = new Node() } };

            // {"Next": {"Next": {"Next": null}}}
            Assert.Equal("A1644E657874A1644E657874A1644E657874F6", Helper.Write(chain));
        }

        // ---- write side: the limit is where it says it is ----

        private static Node BuildChain(int length)
        {
            Node head = new Node();
            Node current = head;

            for (int i = 1; i < length; i++)
            {
                current.Next = new Node();
                current = current.Next;
            }

            return head;
        }

        [Fact]
        public void NestingUpToTheLimitIsWritten()
        {
            // Each Node is one map, so a chain of MaxDepth nodes sits exactly at the limit.
            CborOptions options = new CborOptions { MaxDepth = 8 };

            Helper.Write(BuildChain(8), options);
        }

        [Fact]
        public void NestingBeyondTheLimitThrowsOnWrite()
        {
            CborOptions options = new CborOptions { MaxDepth = 8 };

            CborException exception = Assert.Throws<CborException>(
                () => Helper.Write(BuildChain(9), options));

            Assert.Contains("8", exception.Message);
        }

        [Fact]
        public void RaisingMaxDepthAllowsDeeperData()
        {
            Node deep = BuildChain(200);

            Assert.Throws<CborException>(() => Helper.Write(deep));

            // ... but permitted once the caller opts in.
            Helper.Write(deep, new CborOptions { MaxDepth = 500 });
        }

        [Fact]
        public void DefaultMaxDepthIs64()
        {
            Assert.Equal(64, new CborOptions().MaxDepth);
            Assert.Equal(64, CborWriter.DefaultMaxDepth);
        }

        /// <summary>Nested collections count towards depth too, not just objects.</summary>
        [Fact]
        public void NestedCollectionsAreBounded()
        {
            object nested = new List<object>();
            List<object> current = (List<object>)nested;

            for (int i = 0; i < 100; i++)
            {
                List<object> inner = new List<object>();
                current.Add(inner);
                current = inner;
            }

            Assert.Throws<CborException>(() => Helper.Write(nested));
        }

        // ---- read side ----

        /// <summary>
        /// <c>depth</c> nested definite-length single-element arrays, innermost value 0.
        /// Each <c>0x81</c> is "array(1)", so this is deep nesting in one byte per level.
        /// </summary>
        private static byte[] NestedArrays(int depth)
        {
            byte[] buffer = new byte[depth + 1];

            for (int i = 0; i < depth; i++)
            {
                buffer[i] = 0x81;
            }

            buffer[depth] = 0x00;
            return buffer;
        }

        [Fact]
        public void DeeplyNestedInputThrowsOnReadInsteadOfOverflowingTheStack()
        {
            byte[] hostile = NestedArrays(100_000);

            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<CborValue>(hostile));

            Assert.Contains("nesting depth", exception.Message);
        }

        [Fact]
        public void NestingWithinTheLimitIsRead()
        {
            CborValue value = Cbor.Deserialize<CborValue>(NestedArrays(60));

            Assert.NotNull(value);
        }

        [Fact]
        public void RaisingMaxDepthAllowsDeeperReads()
        {
            byte[] buffer = NestedArrays(200);

            Assert.Throws<CborException>(() => Cbor.Deserialize<CborValue>(buffer));

            CborValue value = Cbor.Deserialize<CborValue>(buffer, new CborOptions { MaxDepth = 500 });
            Assert.NotNull(value);
        }

        /// <summary>
        /// Indefinite-length arrays are the cheaper attack — <c>0x9F</c> with no length to satisfy —
        /// so check them explicitly rather than assuming the definite-length case covers it.
        /// </summary>
        [Fact]
        public void DeeplyNestedIndefiniteArraysAreBoundedOnRead()
        {
            byte[] hostile = new byte[100_000];
            for (int i = 0; i < hostile.Length; i++)
            {
                hostile[i] = 0x9F; // start of indefinite-length array
            }

            Assert.Throws<CborException>(() => Cbor.Deserialize<CborValue>(hostile));
        }

        // ---- the non-generic overloads, whose options argument is optional ----

        /// <summary>
        /// <see cref="Cbor.Serialize(object, Type, in IBufferWriter{byte}, CborOptions)"/> and
        /// <see cref="Cbor.SerializeMultiple(object[], Type, in IBufferWriter{byte}, CborOptions)"/>
        /// take <c>options</c> as an optional argument, so the depth limit must be read only after
        /// <c>CborOptions.Default</c> has been substituted for a null one. Every other test reaches
        /// the writer through the generic overloads, which is why these two carry their own cases.
        /// </summary>
        [Fact]
        public void NonGenericSerializeWithoutOptionsUsesTheDefaultDepth()
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();

            Cbor.Serialize(new Node(), typeof(Node), bufferWriter);

            // {"Next": null}
            Assert.Equal("A1644E657874F6", ToHex(bufferWriter));
        }

        [Fact]
        public void NonGenericSerializeOfNullWithoutOptionsWritesNull()
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();

            Cbor.Serialize(null, typeof(Node), bufferWriter);

            // null
            Assert.Equal("F6", ToHex(bufferWriter));
        }

        [Fact]
        public void NonGenericSerializeMultipleWithoutOptionsUsesTheDefaultDepth()
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();

            Cbor.SerializeMultiple(new object[] { new Node(), new Node() }, typeof(Node), bufferWriter);

            // {"Next": null} {"Next": null}
            Assert.Equal("A1644E657874F6A1644E657874F6", ToHex(bufferWriter));
        }

        [Fact]
        public void NonGenericSerializeWithoutOptionsStillBoundsDepth()
        {
            Node node = new Node();
            node.Next = node;

            using ByteBufferWriter bufferWriter = new ByteBufferWriter();

            Assert.Throws<CborException>(() => Cbor.Serialize(node, typeof(Node), bufferWriter));
        }

        private static string ToHex(ByteBufferWriter bufferWriter)
        {
            return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
        }

        // ---- argument validation ----

        [Fact]
        public void NonPositiveMaxDepthIsRejectedByTheWriter()
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();

            Assert.Throws<ArgumentOutOfRangeException>(() => new CborWriter(bufferWriter, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CborWriter(bufferWriter, -1));
        }

        [Fact]
        public void NonPositiveMaxDepthIsRejectedByTheReader()
        {
            byte[] buffer = new byte[] { 0x00 };

            Assert.Throws<ArgumentOutOfRangeException>(() => new CborReader(buffer.AsSpan(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CborReader(new ReadOnlySequence<byte>(buffer), -1));
        }

        /// <summary>
        /// Depth must be released on the way back out, or a wide-but-shallow document would falsely
        /// trip the limit after enough siblings.
        /// </summary>
        [Fact]
        public void SiblingsDoNotAccumulateDepth()
        {
            List<Node> manySiblings = new List<Node>();
            for (int i = 0; i < 500; i++)
            {
                manySiblings.Add(new Node { Next = new Node() });
            }

            // 500 siblings, each 2 deep, inside one array: nowhere near the limit of 8.
            Helper.Write(manySiblings, new CborOptions { MaxDepth = 8 });
        }

        // ---- the skip path ----

        public class Holder
        {
            public int B { get; set; }
        }

        /// <summary>
        /// A map of two members: <c>"A"</c>, whose value is <paramref name="depth"/> nested arrays, and
        /// <c>"B"</c>, whose value is 1.
        /// </summary>
        private static byte[] UnmappedNesting(int depth)
        {
            byte[] nested = NestedArrays(depth);
            byte[] buffer = new byte[4 + nested.Length + 3];

            buffer[0] = 0xA2;                       // map(2)
            buffer[1] = 0x61; buffer[2] = 0x41;     // "A"
            nested.CopyTo(buffer, 3);
            buffer[3 + nested.Length] = 0x61;       // "B"
            buffer[4 + nested.Length] = 0x42;
            buffer[5 + nested.Length] = 0x01;       // 1

            return buffer;
        }

        /// <summary>
        /// Nesting reached through a member the target type does not declare is bounded too.
        /// </summary>
        /// <remarks>
        /// <c>ObjectConverter</c> hands an unhandled name's value to <c>CborReader.SkipDataItem</c>,
        /// which recurses through the same arrays and maps a read would — so a document that is refused
        /// as a value has to be refused as a skip, or the bound is a bound on the shape of the target
        /// type rather than on the document. At 100 000 levels this took the process down with an
        /// uncatchable <c>StackOverflowException</c>, which is why the modest depth below is the case
        /// that can be asserted at all: a test cannot survive the one that mattered.
        /// </remarks>
        [Fact]
        public void NestingInsideASkippedMemberIsBounded()
        {
            CborException exception = Assert.Throws<CborException>(
                () => Cbor.Deserialize<Holder>(UnmappedNesting(100)));

            Assert.Contains("nesting depth", exception.Message);
        }

        [Fact]
        public void RaisingMaxDepthAllowsADeeperSkippedMember()
        {
            byte[] buffer = UnmappedNesting(100);

            Assert.Throws<CborException>(() => Cbor.Deserialize<Holder>(buffer));

            Holder holder = Cbor.Deserialize<Holder>(buffer, new CborOptions { MaxDepth = 500 });

            Assert.Equal(1, holder.B);
        }

        /// <summary>An unmapped member within the limit is skipped, and the read carries on past it.</summary>
        [Fact]
        public void AShallowSkippedMemberIsUnaffected()
        {
            Holder holder = Cbor.Deserialize<Holder>(UnmappedNesting(8));

            Assert.Equal(1, holder.B);
        }

        /// <summary>
        /// The skip path releases depth like the read path, or a document with enough unmapped members
        /// would trip the limit on the tenth shallow one rather than on anything deep.
        /// </summary>
        [Fact]
        public void SkippedSiblingsDoNotAccumulateDepth()
        {
            using ByteBufferWriter bufferWriter = new ByteBufferWriter();
            CborWriter writer = new CborWriter(bufferWriter);

            writer.WriteBeginMap(201);

            for (int i = 0; i < 200; i++)
            {
                writer.WriteString($"unmapped{i}");
                writer.WriteBeginArray(1);
                writer.WriteBeginArray(1);
                writer.WriteInt32(0);
            }

            writer.WriteString("B");
            writer.WriteInt32(1);

            // 200 unmapped members, each 2 deep, inside one map: 3 levels against a limit of 8.
            Holder holder = Cbor.Deserialize<Holder>(
                bufferWriter.WrittenSpan.ToArray(), new CborOptions { MaxDepth = 8 });

            Assert.Equal(1, holder.B);
        }

        /// <summary>
        /// Read depth and skip depth are one budget, not two. A skipped member nested inside mapped
        /// objects starts from the depth already spent getting to it.
        /// </summary>
        [Fact]
        public void SkipDepthContinuesFromTheDepthAlreadyRead()
        {
            // A ref struct cannot be captured, so no Assert.Throws lambda.
            CborException? thrown = null;

            try
            {
                CborReader reader = new CborReader(NestedArrays(20), maxDepth: 8);
                reader.SkipDataItem();
            }
            catch (CborException exception)
            {
                thrown = exception;
            }

            Assert.NotNull(thrown);
            Assert.Contains("nesting depth", thrown!.Message);
        }

        [Fact]
        public void SkippingWithinTheLimitConsumesTheWholeItem()
        {
            byte[] buffer = new byte[NestedArrays(4).Length + 1];
            NestedArrays(4).CopyTo(buffer, 0);
            buffer[buffer.Length - 1] = 0x01;   // a second item, after the nested one

            CborReader reader = new CborReader(buffer, maxDepth: 8);

            reader.SkipDataItem();

            Assert.Equal(1, reader.ReadInt32());
        }
    }
}
