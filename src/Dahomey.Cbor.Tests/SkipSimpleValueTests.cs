using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A member the target type does not declare is skipped, and skipping consumes exactly one item: it
    /// leaves the reader on whatever follows, or it refuses. The simple values 0-19 and the reserved
    /// additional-information values 29 and 30 have no <see cref="CborPrimitive"/> name, so a switch over
    /// the named ones would leave their header as the current one and hand it to the next read again.
    /// </summary>
    /// <remarks>
    /// Inside an indefinite-length container that is not an error but a loop - the container ends only
    /// when its counter wraps, about 2^32 items later - so the hang cases run on a thread of their own
    /// with a deadline, and a regression fails the test instead of stalling the run. In well-formed data
    /// it is a mis-skip: the member after the simple value is read from the wrong position.
    /// <para>
    /// What is skipped and what is refused follows RFC 8949 §3.3 and <see cref="CborDataItemScanner"/>,
    /// which already holds the library's view of well-formedness: simple values 0-19 and 32-255 are
    /// valid and unassigned, so skipping them is forward compatibility; a two-byte simple value below 32,
    /// the reserved values 29 and 30 and a break code where an item belongs are not well-formed, so they
    /// are a <see cref="CborException"/> and never a value. The one place the reader and the scanner
    /// differ is additional information 28 on major type 7, which is the sixteen-byte
    /// <see cref="decimal"/> this library writes by default.
    /// </para>
    /// </remarks>
    public class SkipSimpleValueTests
    {
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

        public class Holder
        {
            public int A { get; set; }
        }

        private delegate Task<Holder> ReadDocument(byte[] bytes);

        /// <summary>
        /// Every public way in. The single-item stream row is an exposable <see cref="MemoryStream"/>,
        /// which is read as a span of exactly its bytes: <c>DeserializeAsync</c> over any other seekable
        /// stream reads from the whole rented buffer, which is longer than the document, so it cannot say
        /// where a truncated document ends.
        /// </summary>
        private static IEnumerable<(string Name, ReadDocument Read)> EntryPoints()
        {
            yield return ("Deserialize(ReadOnlySpan)",
                bytes => Task.FromResult(Cbor.Deserialize<Holder>(bytes.AsSpan())));

            yield return ("Deserialize(ReadOnlySequence)",
                bytes => Task.FromResult(Cbor.Deserialize<Holder>(new ReadOnlySequence<byte>(bytes))));

            yield return ("Deserialize(ReadOnlySequence, one byte per segment)",
                bytes => Task.FromResult(Cbor.Deserialize<Holder>(Helper.Fragmentize(bytes))));

            yield return ("DeserializeAsync(Stream)",
                async bytes => await Cbor.DeserializeAsync<Holder>(
                    new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true)));

            yield return ("DeserializeAsync(PipeReader)",
                async bytes => await Cbor.DeserializeAsync<Holder>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes))));

            yield return ("DeserializeAsync(PipeReader, one byte per segment)",
                async bytes => await Cbor.DeserializeAsync<Holder>(PipeReader.Create(Helper.Fragmentize(bytes))));

            yield return ("DeserializeMultiple(ReadOnlySpan)",
                bytes => Task.FromResult(Cbor.DeserializeMultiple<Holder>(bytes.AsSpan())[0]));

            yield return ("DeserializeMultiple(ReadOnlySequence)",
                bytes => Task.FromResult(Cbor.DeserializeMultiple<Holder>(new ReadOnlySequence<byte>(bytes))[0]));

            yield return ("DeserializeMultipleAsync(Stream)",
                async bytes => (await Cbor.DeserializeMultipleAsync<Holder>(new MemoryStream(bytes)))[0]);

            yield return ("DeserializeMultipleAsync(PipeReader)",
                async bytes => (await Cbor.DeserializeMultipleAsync<Holder>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes))))[0]);

            yield return ("ReadNextItemAsync(PipeReader)",
                async bytes => await Cbor.ReadNextItemAsync<Holder>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes))));
        }

        private static IEnumerable<object[]> Rows(params (string Hex, int Expected)[] documents)
        {
            foreach ((string hex, int expected) in documents)
            {
                yield return new object[] { hex, expected };
            }
        }

        private static IEnumerable<object[]> Rows(params string[] documents)
        {
            foreach (string hex in documents)
            {
                yield return new object[] { hex };
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Well-formed documents: the simple value is skipped and the member after it is still read.
        // ---------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> WellFormedDocuments()
        {
            return Rows(
                // The reported desync: a simple value inside an array, then the member after it.
                ("A2617A82E305614107", 7),             // {"z": [simple(3), 5], "A": 7}
                ("A2617AF861614105", 5),               // {"z": simple(97), "A": 5}

                // Unassigned simple values on their own, the edges of the range.
                ("A2617AE0614107", 7),                 // {"z": simple(0), "A": 7}
                ("A2617AF3614107", 7),                 // {"z": simple(19), "A": 7}
                ("A2617AF820614107", 7),               // {"z": simple(32), "A": 7}
                ("A2617AF8FF614107", 7),               // {"z": simple(255), "A": 7}

                // The values with a name keep working beside them.
                ("A2617AF4614107", 7),                 // {"z": false, "A": 7}
                ("A2617AF5614107", 7),                 // {"z": true, "A": 7}
                ("A2617AF6614107", 7),                 // {"z": null, "A": 7}
                ("A2617AF7614107", 7),                 // {"z": undefined, "A": 7}

                // Inside indefinite-length containers, where the stale header used to loop.
                ("A2617A9FE3E4FF614107", 7),           // {"z": [_ simple(3), simple(4)], "A": 7}
                ("A2617ABF6141E3FF614107", 7),         // {"z": {_ "A": simple(3)}, "A": 7}
                ("A2617A9FF861FF614107", 7),           // {"z": [_ simple(97)], "A": 7}
                ("A2617A839FE3FF9FE3FF9FE3FF614107", 7), // {"z": [[_ simple(3)], [_ simple(3)], [_ simple(3)]], "A": 7}

                // Simple values as map keys, and under a semantic tag.
                ("A2617AA1E3E4614107", 7),             // {"z": {simple(3): simple(4)}, "A": 7}
                ("A2617AC1E3614107", 7),               // {"z": 1(simple(3)), "A": 7}

                // The sixteen-byte decimal this library writes is the one extension that stays.
                ("A2617AFC00000000000000000000000000000000614107", 7) // {"z": decimal 0, "A": 7}
            );
        }

        [Theory]
        [MemberData(nameof(WellFormedDocuments))]
        public async Task AWellFormedSimpleValueIsSkippedAndTheMemberAfterItIsReadAsync(string hex, int expected)
        {
            byte[] bytes = hex.HexToBytes();

            foreach ((string name, ReadDocument read) in EntryPoints())
            {
                Outcome outcome = await RunAsync(read, bytes);

                Assert.True(outcome.Completed, $"{name} did not finish within {Deadline.TotalSeconds} s on {hex}");
                Assert.True(outcome.Error == null,
                    $"{name} threw {outcome.Error?.GetType().Name} \"{outcome.Error?.Message}\" on {hex}");
                Assert.True(expected == outcome.Value.A, $"{name} read A = {outcome.Value.A} on {hex}, expected {expected}");
            }
        }

        // ---------------------------------------------------------------------------------------------
        // Malformed documents: a CborException with a path, never a value and never a hang.
        // ---------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> MalformedDocuments()
        {
            return Rows(
                // The four documents reported as hangs - each an indefinite or very long container that
                // never ends, so the reader runs out of bytes, and used to spend 3-30 s getting there.
                "A1617A9FE3",                             // {"z": [_ simple(3) ... no break
                "A1617A9A7FFFFFFFE3",                     // {"z": [2147483647 items: simple(3) ... ends
                "A1617ABF6141E3",                         // {"z": {_ "A": simple(3) ... no break
                "A1617A839FE39FE39FE3",                   // {"z": [[_ simple(3) ..., [_ simple(3) ..., [_ simple(3) ... ends

                // Reserved additional information, where a stale header used to be re-presented.
                "A1617A9FFD",                             // {"z": [_ <29> ...
                "A1617A9FFE",                             // {"z": [_ <30> ...
                "A2617AFD614107",                         // {"z": <29>, "A": 7}
                "A2617AFE614107",                         // {"z": <30>, "A": 7}
                "A2617A82FD05614107",                     // {"z": [<29>, 5], "A": 7}

                // A two-byte simple value below 32 is not well-formed (RFC 8949 §3.3).
                "A2617AF800614107",                       // {"z": F8 00, "A": 7}
                "A2617AF81F614107",                       // {"z": F8 1F, "A": 7}
                "A1617AF8",                               // {"z": F8 ... no argument byte

                // A break code is the end of an indefinite container, not a value.
                "A1617AFF",                               // {"z": break
                "A2617A8201FF614107",                     // {"z": [1, break], "A": 7}
                "A2617ABF6141FF614107",                   // {"z": {_ "A": break ... a map ending between a key and its value
                "A2617AC1FF614107"                        // {"z": 1(break), "A": 7}
            );
        }

        [Theory]
        [MemberData(nameof(MalformedDocuments))]
        public async Task AMalformedSimpleValueIsRefusedAsACborExceptionAsync(string hex)
        {
            await AssertEveryEntryPointRefusesAsync(hex);
        }

        public static IEnumerable<object[]> TruncatedFloatDocuments()
        {
            return Rows(
                "A1617AFB4164",                           // {"z": float64 cut after 2 of 8 bytes
                "A1617AFA4164",                           // {"z": float32 cut after 2 of 4 bytes
                "A1617AF941",                             // {"z": float16 cut after 1 of 2 bytes
                "A1617AFC0000000000000000",               // {"z": decimal cut after 8 of 16 bytes
                "A1617A81FB"                              // {"z": [float64 with no bytes at all
            );
        }

        /// <summary>
        /// A float is stepped over by its length, so the length has to be checked against what is left:
        /// the span reader never looks at the position again and would take a document that ends inside
        /// the float, and the sequence reader cannot advance past its end and would throw an
        /// <see cref="ArgumentOutOfRangeException"/> instead of a <see cref="CborException"/>.
        /// </summary>
        [Theory]
        [MemberData(nameof(TruncatedFloatDocuments))]
        public async Task ATruncatedFloatInASkippedValueIsACborExceptionOnEveryEntryPointAsync(string hex)
        {
            await AssertEveryEntryPointRefusesAsync(hex);
        }

        private static async Task AssertEveryEntryPointRefusesAsync(string hex)
        {
            byte[] bytes = hex.HexToBytes();

            foreach ((string name, ReadDocument read) in EntryPoints())
            {
                Outcome outcome = await RunAsync(read, bytes);

                Assert.True(outcome.Completed, $"{name} did not finish within {Deadline.TotalSeconds} s on {hex}");
                Assert.True(outcome.Error != null,
                    $"{name} read A = {outcome.Value?.A} on {hex} instead of refusing it");

                CborException exception = Assert.IsAssignableFrom<CborException>(outcome.Error);

                Assert.True(exception.Path != null && exception.Path.StartsWith("$", StringComparison.Ordinal),
                    $"{name} gave \"{exception.Path}\" as the path of \"{exception.Message}\" on {hex}");
            }
        }

        // ---------------------------------------------------------------------------------------------
        // The reader on its own, over the three shapes it can be built on.
        // ---------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> UnassignedSimpleValues()
        {
            for (int simpleValue = 0; simpleValue < 20; simpleValue++)
            {
                yield return new object[] { (0xE0 + simpleValue).ToString("X2") };
            }

            yield return new object[] { "F820" };           // simple(32), the first two-byte value
            yield return new object[] { "F8FF" };           // simple(255)
        }

        [Theory]
        [MemberData(nameof(UnassignedSimpleValues))]
        public void SkipDataItemTakesExactlyAnUnassignedSimpleValue(string simpleValue)
        {
            OnEveryReaderShape(simpleValue + "05", (ref CborReader reader) =>
            {
                reader.SkipDataItem();

                Assert.Equal(5, reader.ReadInt32());
                Assert.False(reader.DataAvailable);
            });
        }

        [Theory]
        [InlineData("FF", "break")]
        [InlineData("FD", "reserved 29")]
        [InlineData("FE", "reserved 30")]
        [InlineData("F800", "two-byte simple value 0")]
        [InlineData("F81F", "two-byte simple value 31")]
        [InlineData("F8", "two-byte simple value with no argument")]
        [InlineData("F941", "half float cut short")]
        [InlineData("FA4164", "single float cut short")]
        [InlineData("FB4164", "double float cut short")]
        [InlineData("FC0000000000000000", "decimal cut short")]
        [InlineData("C1FF", "break under a tag")]
        public void SkipDataItemRefusesWhatIsNotAWellFormedItem(string hexBuffer, string why)
        {
            OnEveryReaderShape(hexBuffer, (ref CborReader reader) =>
            {
                // A ref struct cannot be captured, so no Assert.Throws lambda.
                CborException thrown = null;

                try
                {
                    reader.SkipDataItem();
                }
                catch (CborException exception)
                {
                    thrown = exception;
                }

                Assert.True(thrown != null, $"{why}: skipping {hexBuffer} did not throw a CborException");
            });
        }

        // ---------------------------------------------------------------------------------------------
        // Every initial byte, in a skipped position.
        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// 0xFF, and the reserved additional information 28-30 on every major type but 7; 31 on the
        /// majors that have no indefinite form (0, 1 and 6); 29 and 30 on major type 7.
        /// </summary>
        private static bool IsWellFormedInitialByte(int initialByte)
        {
            int majorType = initialByte >> 5;
            int additionalInformation = initialByte & 0x1F;

            if (additionalInformation is 28 or 29 or 30)
            {
                // Major type 7 carries the library's own decimal in 28.
                return majorType == 7 && additionalInformation == 28;
            }

            if (additionalInformation == 31)
            {
                return majorType is 2 or 3 or 4 or 5;
            }

            return true;
        }

        /// <summary>
        /// A complete item that starts with this byte: the argument bytes it asks for, then whatever its
        /// major type puts behind them - an empty payload, a run of one-byte items, or the item a tag
        /// tags. The argument of an unbounded or oversized length is zero, so the item stays small.
        /// </summary>
        private static byte[] ItemStartingWith(int initialByte)
        {
            int majorType = initialByte >> 5;
            int additionalInformation = initialByte & 0x1F;

            List<byte> item = new List<byte> { (byte)initialByte };

            if (majorType == 7)
            {
                int argumentLength = additionalInformation switch
                {
                    24 => 1,
                    25 => 2,
                    26 => 4,
                    27 => 8,
                    28 => 16,
                    _ => 0,
                };

                item.AddRange(new byte[argumentLength]);

                // simple(32): the first value a two-byte form may carry.
                if (additionalInformation == 24)
                {
                    item[1] = 0x20;
                }

                return item.ToArray();
            }

            int argumentBytes = additionalInformation switch { 24 => 1, 25 => 2, 26 => 4, 27 => 8, _ => 0 };
            item.AddRange(new byte[argumentBytes]);

            // Reserved and unbounded forms: nothing sensible follows except the break an indefinite
            // container needs and the item a tag needs.
            if (additionalInformation >= 28)
            {
                if (additionalInformation == 31 && majorType is 2 or 3 or 4 or 5)
                {
                    item.Add(0xFF);
                }

                return item.ToArray();
            }

            int count = additionalInformation < 24 ? additionalInformation : 0;

            switch (majorType)
            {
                case 2:
                case 3:
                    item.AddRange(new byte[count]);
                    break;

                case 4:
                    for (int i = 0; i < count; i++)
                    {
                        item.Add(0x01);
                    }
                    break;

                case 5:
                    for (int i = 0; i < count; i++)
                    {
                        item.Add((byte)i);      // distinct keys: nothing here is about duplicates
                        item.Add(0x01);
                    }
                    break;

                case 6:
                    item.Add(0x01);
                    break;
            }

            return item.ToArray();
        }

        private static byte[] DocumentSkipping(byte[] item)
        {
            // {"z": <item>, "A": 7}
            List<byte> document = new List<byte> { 0xA2, 0x61, 0x7A };
            document.AddRange(item);
            document.AddRange(new byte[] { 0x61, 0x41, 0x07 });
            return document.ToArray();
        }

        private static string Classify(Func<Holder> read)
        {
            try
            {
                return $"A = {read().A}";
            }
            catch (CborException)
            {
                return "CborException";
            }
            catch (Exception exception)
            {
                return exception.GetType().Name;
            }
        }

        /// <summary>
        /// All 256 initial bytes, each as the value of an unmapped member: it is skipped and the member
        /// after it is read, or it is a <see cref="CborException"/>, and the three reader shapes agree
        /// about which. Which is expected is RFC 8949 §3.3 and §3.2, and cross-checked against
        /// <see cref="CborDataItemScanner"/>.
        /// </summary>
        [Fact]
        public void EveryInitialByteIsSkippedOrRefusedTheSameWayOnEveryReaderShape()
        {
            int skipped = 0;
            List<string> disagreements = new List<string>();

            for (int initialByte = 0; initialByte < 256; initialByte++)
            {
                byte[] item = ItemStartingWith(initialByte);
                byte[] document = DocumentSkipping(item);

                string span = Classify(() => Cbor.Deserialize<Holder>(document.AsSpan()));
                string sequence = Classify(() => Cbor.Deserialize<Holder>(new ReadOnlySequence<byte>(document)));
                string fragmented = Classify(() => Cbor.Deserialize<Holder>(Helper.Fragmentize(document)));

                bool wellFormed = IsWellFormedInitialByte(initialByte);
                string expected = wellFormed ? "A = 7" : "CborException";

                if (wellFormed)
                {
                    skipped++;
                }

                if (span != expected || sequence != expected || fragmented != expected)
                {
                    disagreements.Add($"{initialByte:X2} ({Convert.ToHexString(item)}): span {span}, sequence {sequence}, "
                        + $"fragmented {fragmented}, expected {expected}");
                }

                // The scanner is the library's other definition of well-formed. The two agree on every
                // byte but the library's own decimal.
                ReadOnlySpan<byte> scanned = item;
                bool scannerAccepts = CborDataItemScanner.TryReadDataItem(ref scanned, out _) && scanned.Length == 0;

                if (initialByte == 0xFC)
                {
                    Assert.False(scannerAccepts);
                }
                else
                {
                    Assert.True(scannerAccepts == wellFormed,
                        $"{initialByte:X2}: the scanner says {scannerAccepts}, the table says {wellFormed}");
                }
            }

            Assert.Empty(disagreements);

            // 256, less additional information 28-30 on the seven major types 0-6 (21), less 31 on the
            // three that have no indefinite form (3), less FD, FE and FF (3). FC stays: it is the decimal.
            Assert.Equal(229, skipped);
        }

        [Fact]
        public void ATwoByteSimpleValueBelow32IsRefusedAndFromThere32IsSkipped()
        {
            for (int argument = 0; argument < 256; argument++)
            {
                byte[] document = DocumentSkipping(new byte[] { 0xF8, (byte)argument });

                string span = Classify(() => Cbor.Deserialize<Holder>(document.AsSpan()));
                string fragmented = Classify(() => Cbor.Deserialize<Holder>(Helper.Fragmentize(document)));

                string expected = argument < 32 ? "CborException" : "A = 7";

                Assert.True(expected == span && expected == fragmented,
                    $"F8 {argument:X2}: span {span}, fragmented {fragmented}, expected {expected}");
            }
        }

        // ---------------------------------------------------------------------------------------------

        private delegate void ReaderProbe(ref CborReader reader);

        private static void OnEveryReaderShape(string hexBuffer, ReaderProbe probe)
        {
            byte[] buffer = hexBuffer.HexToBytes();

            CborReader spanReader = new CborReader(buffer.AsSpan());
            probe(ref spanReader);

            CborReader sequenceReader = new CborReader(new ReadOnlySequence<byte>(buffer));
            probe(ref sequenceReader);

            CborReader fragmentedReader = new CborReader(Helper.Fragmentize(buffer));
            probe(ref fragmentedReader);
        }

        private sealed class Outcome
        {
            public bool Completed;
            public Holder Value;
            public Exception Error;
        }

        /// <summary>
        /// Runs a read on a thread of its own and gives it <see cref="Deadline"/>. A read that never
        /// ends then fails the test that started it rather than the run, and its thread - a background
        /// one - does not keep the test host alive.
        /// </summary>
        private static async Task<Outcome> RunAsync(ReadDocument read, byte[] bytes)
        {
            Task<Outcome> task = Task.Factory.StartNew(async () =>
            {
                Outcome outcome = new Outcome { Completed = true };

                try
                {
                    outcome.Value = await read(bytes);
                }
                catch (Exception exception)
                {
                    outcome.Error = exception;
                }

                return outcome;
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

            Task finished = await Task.WhenAny(task, Task.Delay(Deadline));

            return finished == task ? await task : new Outcome { Completed = false };
        }
    }
}
