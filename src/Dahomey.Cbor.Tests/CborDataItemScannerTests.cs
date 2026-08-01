using System;
using System.Collections.Generic;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Covers <see cref="CborDataItemScanner"/>: determining a data item's length without decoding it
    /// and without throwing on truncated input — the primitive needed for CBOR sequences (RFC 8742,
    /// issue #139) and for streaming (issue #134).
    /// </summary>
    public class CborDataItemScannerTests
    {
        private static CborDataItemStatus Scan(string hexBuffer, out int length)
        {
            return CborDataItemScanner.Scan(hexBuffer.HexToBytes(), out length);
        }

        [Theory]
        // scalars
        [InlineData("00", 1)]                       // 0
        [InlineData("17", 1)]                       // 23
        [InlineData("1818", 2)]                     // 24
        [InlineData("190100", 3)]                   // 256
        [InlineData("1A000F4240", 5)]               // 1000000
        [InlineData("1B0000000100000000", 9)]       // 4294967296
        [InlineData("20", 1)]                       // -1
        [InlineData("3903E7", 3)]                   // -1000
        [InlineData("F4", 1)]                       // false
        [InlineData("F5", 1)]                       // true
        [InlineData("F6", 1)]                       // null
        [InlineData("F7", 1)]                       // undefined
        [InlineData("F90001", 3)]                   // half 5.960464477539063e-8
        [InlineData("FA47C35000", 5)]               // float 100000.0
        [InlineData("FB3FD5555555555555", 9)]       // double 1/3
        [InlineData("F820", 2)]                     // simple(32)
        // strings
        [InlineData("40", 1)]                       // h''
        [InlineData("4401020304", 5)]               // h'01020304'
        [InlineData("60", 1)]                       // ""
        [InlineData("6449455446", 5)]               // "IETF"
        // arrays and maps
        [InlineData("80", 1)]                       // []
        [InlineData("83010203", 4)]                 // [1, 2, 3]
        [InlineData("8301820203820405", 8)]         // [1, [2, 3], [4, 5]]
        [InlineData("A0", 1)]                       // {}
        [InlineData("A201020304", 5)]               // {1: 2, 3: 4}
        [InlineData("A26161016162820203", 9)]       // {"a": 1, "b": [2, 3]}
        // tags
        [InlineData("C074323031332D30332D32315432303A30343A30305A", 22)] // 0("2013-03-21T20:04:00Z")
        [InlineData("D82763666F6F", 6)]             // 39("foo")
        // indefinite lengths
        [InlineData("5F42010243030405FF", 9)]       // (_ h'0102', h'030405')
        [InlineData("7F657374726561646D696E67FF", 13)] // (_ "strea", "ming")
        [InlineData("9FFF", 2)]                     // [_ ]
        [InlineData("9F018202039F0203FFFF", 10)]    // [_ 1, [2, 3], [_ 2, 3]]
        [InlineData("BF61610161629F0203FFFF", 11)]  // {_ "a": 1, "b": [_ 2, 3]}
        public void CompleteItems(string hexBuffer, int expectedLength)
        {
            Assert.Equal(CborDataItemStatus.Complete, Scan(hexBuffer, out int length));
            Assert.Equal(expectedLength, length);
        }

        /// <summary>
        /// Every strict prefix of a complete item must report Incomplete — never Malformed and never
        /// a bogus Complete. This is the property that makes the scanner usable on a stream, so it is
        /// checked exhaustively rather than by example.
        /// </summary>
        [Theory]
        [InlineData("1818")]
        [InlineData("1B0000000100000000")]
        [InlineData("4401020304")]
        [InlineData("6449455446")]
        [InlineData("83010203")]
        [InlineData("A26161016162820203")]
        [InlineData("C074323031332D30332D32315432303A30343A30305A")]
        [InlineData("D82763666F6F")]
        [InlineData("5F42010243030405FF")]
        [InlineData("7F657374726561646D696E67FF")]
        [InlineData("9F018202039F0203FFFF")]
        [InlineData("BF61610161629F0203FFFF")]
        [InlineData("FB3FD5555555555555")]
        public void EveryStrictPrefixIsIncomplete(string hexBuffer)
        {
            byte[] complete = hexBuffer.HexToBytes();

            for (int prefixLength = 0; prefixLength < complete.Length; prefixLength++)
            {
                CborDataItemStatus status = CborDataItemScanner.Scan(
                    complete.AsSpan(0, prefixLength), out _);

                Assert.Equal(CborDataItemStatus.Incomplete, status);
            }

            Assert.Equal(CborDataItemStatus.Complete, CborDataItemScanner.Scan(complete, out int length));
            Assert.Equal(complete.Length, length);
        }

        [Fact]
        public void EmptyBufferIsIncomplete()
        {
            Assert.Equal(CborDataItemStatus.Incomplete, CborDataItemScanner.Scan(default, out int length));
            Assert.Equal(0, length);
        }

        [Theory]
        [InlineData("1C")]   // additional info 28, reserved
        [InlineData("1D")]   // 29, reserved
        [InlineData("1E")]   // 30, reserved
        [InlineData("FF")]   // stray break where an item was expected
        [InlineData("1F")]   // indefinite length on major type 0
        [InlineData("3F")]   // indefinite length on major type 1
        [InlineData("DF")]   // indefinite length on major type 6 (tag)
        [InlineData("5F00FF")]     // indefinite byte string with a non-string chunk
        [InlineData("5F6161FF")]   // indefinite byte string with a *text* chunk
        [InlineData("7F4101FF")]   // indefinite text string with a *byte* chunk
        [InlineData("5F5F41014102FFFF")] // nested indefinite chunk
        [InlineData("BF6161FF")]   // indefinite map ending between key and value
        public void MalformedItems(string hexBuffer)
        {
            Assert.Equal(CborDataItemStatus.Malformed, Scan(hexBuffer, out _));
        }

        /// <summary>
        /// A hostile buffer can describe unbounded nesting in as many bytes as it has: each 0x9F
        /// opens an indefinite array. Without a depth limit this is a stack overflow, so it must be
        /// refused — and refused distinguishably from malformed input.
        /// </summary>
        [Fact]
        public void ExcessiveNestingIsRefusedRatherThanOverflowingTheStack()
        {
            byte[] deeplyNested = new byte[100_000];
            for (int i = 0; i < deeplyNested.Length; i++)
            {
                deeplyNested[i] = 0x9F; // start of indefinite-length array
            }

            Assert.Equal(CborDataItemStatus.TooDeep, CborDataItemScanner.Scan(deeplyNested, out _));
        }

        [Fact]
        public void NestingUpToTheLimitIsAccepted()
        {
            // maxDepth counts the outermost item as depth 1, so `depth` nested arrays need maxDepth
            // of exactly `depth`.
            const int depth = 10;
            List<byte> bytes = new List<byte>();
            for (int i = 0; i < depth - 1; i++)
            {
                bytes.Add(0x81); // array(1)
            }
            bytes.Add(0x00); // innermost: 0

            Assert.Equal(
                CborDataItemStatus.Complete,
                CborDataItemScanner.Scan(bytes.ToArray(), depth, out int length));
            Assert.Equal(depth, length);

            Assert.Equal(
                CborDataItemStatus.TooDeep,
                CborDataItemScanner.Scan(bytes.ToArray(), depth - 1, out _));
        }

        [Fact]
        public void NonPositiveMaxDepthIsRejected()
        {
            byte[] buffer = "00".HexToBytes();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => CborDataItemScanner.Scan(buffer, 0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CborDataItemScanner.Scan(buffer, -1, out _));
        }

        /// <summary>
        /// A length that cannot possibly be satisfied reports Incomplete, not Malformed: the scanner
        /// cannot know the stream will not eventually supply the bytes, and must not allocate or
        /// overflow while deciding.
        /// </summary>
        [Fact]
        public void AbsurdlyLargeStringLengthIsIncompleteNotFatal()
        {
            // bytes(0xFFFFFFFFFFFFFFFF) with no payload
            Assert.Equal(
                CborDataItemStatus.Incomplete,
                Scan("5BFFFFFFFFFFFFFFFF", out _));

            // array with a huge declared item count
            Assert.Equal(
                CborDataItemStatus.Incomplete,
                Scan("9BFFFFFFFFFFFFFFFF", out _));
        }

        // ---- sequence reading (RFC 8742) ----

        [Fact]
        public void TryReadDataItemWalksASequence()
        {
            // 1, "two", [3, 4] concatenated with no framing — a CBOR sequence
            ReadOnlySpan<byte> remaining = "016374776F820304".HexToBytes();
            List<string> items = new List<string>();

            while (CborDataItemScanner.TryReadDataItem(ref remaining, out ReadOnlySpan<byte> item))
            {
                items.Add(item.BytesToHex().ToUpperInvariant());
            }

            Assert.Equal(new[] { "01", "6374776F", "820304" }, items);
            Assert.True(remaining.IsEmpty);
        }

        [Fact]
        public void TryReadDataItemLeavesTheIncompleteTailInPlace()
        {
            // 1, then a truncated text string header promising 3 bytes but supplying 1
            byte[] buffer = "01637400".HexToBytes();
            ReadOnlySpan<byte> remaining = buffer;

            Assert.True(CborDataItemScanner.TryReadDataItem(ref remaining, out ReadOnlySpan<byte> first));
            Assert.Equal("01", first.BytesToHex().ToUpperInvariant());

            // The tail is not consumed, so the caller can prepend it to the next read.
            Assert.False(CborDataItemScanner.TryReadDataItem(ref remaining, out _));
            Assert.Equal(3, remaining.Length);
            Assert.Equal("637400", remaining.BytesToHex().ToUpperInvariant());
        }

        [Fact]
        public void ScanSequenceCountsCompleteItems()
        {
            Assert.Equal(
                CborDataItemStatus.Complete,
                CborDataItemScanner.ScanSequence("016374776F820304".HexToBytes(), out int consumed, out int count));
            Assert.Equal(8, consumed);
            Assert.Equal(3, count);
        }

        [Fact]
        public void ScanSequenceReportsATruncatedTail()
        {
            // 1, "two", then a truncated array(2) missing its second item
            Assert.Equal(
                CborDataItemStatus.Incomplete,
                CborDataItemScanner.ScanSequence("016374776F8203".HexToBytes(), out int consumed, out int count));

            Assert.Equal(5, consumed); // "01" + "6374776F"
            Assert.Equal(2, count);
        }

        [Fact]
        public void ScanSequenceReportsMalformedRemainder()
        {
            Assert.Equal(
                CborDataItemStatus.Malformed,
                CborDataItemScanner.ScanSequence("011C".HexToBytes(), out int consumed, out int count));

            Assert.Equal(1, consumed);
            Assert.Equal(1, count);
        }

        [Fact]
        public void EmptySequenceIsComplete()
        {
            Assert.Equal(
                CborDataItemStatus.Complete,
                CborDataItemScanner.ScanSequence(default, out int consumed, out int count));
            Assert.Equal(0, consumed);
            Assert.Equal(0, count);
        }

        /// <summary>
        /// The scanner's lengths must agree with what the decoder actually consumes, otherwise a
        /// sequence walk would drift. Cross-checked against real serializer output.
        /// </summary>
        [Fact]
        public void ScannedLengthAgreesWithSerializerOutput()
        {
            string[] hexBuffers =
            {
                Helper.Write(42),
                Helper.Write("hello"),
                Helper.Write(new[] { 1, 2, 3 }),
                Helper.Write(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }),
                Helper.Write(1.0 / 3.0),
            };

            foreach (string hexBuffer in hexBuffers)
            {
                byte[] bytes = hexBuffer.HexToBytes();

                Assert.Equal(CborDataItemStatus.Complete, CborDataItemScanner.Scan(bytes, out int length));
                Assert.Equal(bytes.Length, length);
            }
        }
    }
}
