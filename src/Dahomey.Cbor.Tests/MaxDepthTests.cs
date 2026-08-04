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
    }
}
