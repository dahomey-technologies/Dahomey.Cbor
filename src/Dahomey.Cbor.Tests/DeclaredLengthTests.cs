using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A definite-length array announces how many items it holds before holding any, and a reader that
    /// believes the announcement spends memory on it. Nine bytes can announce 2^63 items, so the count is
    /// a claim to check against the bytes that are actually there rather than a size to allocate: every
    /// CBOR data item is at least one byte, so a count beyond the bytes remaining cannot be satisfied.
    /// </summary>
    /// <remarks>
    /// Two things are asserted for every lying count, because either alone is satisfied by a read that
    /// fails slowly. The failure is a <see cref="CborException"/> with a path - the contract for hostile
    /// input, which <c>9A 7FFFFFFF</c> broke by escaping as an <see cref="OutOfMemoryException"/> - and
    /// the thread allocates next to nothing getting there, which <c>9A 10000000</c> broke by reserving a
    /// gibibyte before it failed with the right exception type.
    /// <para>
    /// Allocation is read off the thread rather than the process, so the reads here are all driven to
    /// completion on the calling thread: a pipe or stream built over memory completes synchronously.
    /// </para>
    /// </remarks>
    public class DeclaredLengthTests
    {
        // Far below the smallest allocation a trusted count could cost: an int[1_000_000] is 4 MB. What
        // is actually spent is the exception, its stack trace and its message.
        private const long AllocationBudget = 256 * 1024;

        private delegate Task<object> ReadAsync(byte[] bytes, CborOptions options);

        public class LengthItem
        {
            public int Id { get; set; }
        }

        public class LengthHolder
        {
            public int[] Numbers { get; set; }
            public List<int> Values { get; set; }
            public LengthItem[] Items { get; set; }
        }

        /// <summary>The counts an audit found to matter, written as the bytes that announce them.</summary>
        public static IEnumerable<object[]> LyingLengths()
        {
            yield return new object[] { "1e6", "9A000F4240" };
            yield return new object[] { "1e7", "9A00989680" };
            yield return new object[] { "1e8", "9A05F5E100" };
            yield return new object[] { "2^28", "9A10000000" };           // a gibibyte of int[]
            yield return new object[] { "2^31-1", "9A7FFFFFFF" };         // past Array.MaxLength
            yield return new object[] { "2^32-1", "9AFFFFFFFF" };
            yield return new object[] { "2^32", "9B0000000100000000" };
            yield return new object[] { "2^63", "9B8000000000000000" };
            yield return new object[] { "2^64-1", "9BFFFFFFFFFFFFFFFF" };

            // A few real items behind the claim: not an empty buffer, still nowhere near enough.
            yield return new object[] { "1e6 and three items", "9A000F4240" + "010203" };
            yield return new object[] { "2^28 and three items", "9A10000000" + "010203" };
        }

        private static IEnumerable<(string Name, ReadAsync Read)> ArrayShapes()
        {
            yield return Shape<int[]>("int[]");
            yield return Shape<long[]>("long[]");
            yield return Shape<string[]>("string[]");
            yield return Shape<object[]>("object[]");
            yield return Shape<LengthItem[]>("class[]");
            yield return Shape<List<int>>("List<int>");
            yield return Shape<List<LengthItem>>("List<class>");
            yield return Shape<IList<int>>("IList<int>");
            yield return Shape<ImmutableArray<int>>("ImmutableArray<int>");
            yield return Shape<ImmutableList<string>>("ImmutableList<string>");
            yield return Shape<CborValue>("CborValue");
            yield return Shape<CborArray>("CborArray");
        }

        public static IEnumerable<object[]> EveryShapeAndLength()
        {
            foreach ((string name, ReadAsync read) in ArrayShapes())
            {
                foreach (object[] lie in LyingLengths())
                {
                    yield return new object[] { name, lie[0], lie[1], read };
                }
            }
        }

        public static IEnumerable<object[]> EveryEntryPoint()
        {
            foreach (object[] row in EntryPointsFor<int[]>("int[]"))
            {
                yield return row;
            }

            foreach (object[] row in EntryPointsFor<CborValue>("CborValue"))
            {
                yield return row;
            }
        }

        [Theory]
        [MemberData(nameof(EveryShapeAndLength))]
        public void ALyingCountIsRefusedWithoutBeingAllocated(string shape, string lie, string hex, object read)
        {
            CborException exception = Refuse((ReadAsync)read, hex, new CborOptions(), out long allocated);

            Assert.Equal("$", exception.Path);
            Assert.True(allocated < AllocationBudget,
                $"{shape} reading a count of {lie} allocated {allocated:N0} bytes before refusing it.");
        }

        /// <summary>
        /// The same refusal from every public way in. The doors differ in how they get the bytes - a
        /// span, a segmented sequence, a stream read into a buffer, a pipe, a speculative read of a pipe -
        /// and a bound that held at some of them would leave the others allocating.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryEntryPoint))]
        public void EveryEntryPointRefusesALyingCountWithoutAllocatingIt(string door, object read)
        {
            CborException exception = Refuse((ReadAsync)read, "9A10000000", new CborOptions(), out long allocated);

            Assert.Equal("$", exception.Path);
            Assert.True(allocated < AllocationBudget,
                $"{door} allocated {allocated:N0} bytes reading a count of 2^28.");
        }

        /// <summary>
        /// The refusal names where in the document the claim was made, as every other failure of a read
        /// does: <c>$.Numbers</c> and not just <c>$</c>.
        /// </summary>
        [Theory]
        [InlineData("A1674E756D62657273" + "9A10000000", "$.Numbers")]      // {"Numbers": [2^28 items...
        [InlineData("A1665661" + "6C756573" + "9A10000000", "$.Values")]     // {"Values": [2^28 items...
        [InlineData("A1654974656D73" + "9A7FFFFFFF", "$.Items")]             // {"Items": [2^31-1 items...
        public void ALyingCountInsideAnObjectNamesItsMember(string hex, string expectedPath)
        {
            Assert.Equal(expectedPath, Refuse(
                (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<LengthHolder>(bytes, options)),
                hex, new CborOptions(), out long allocated).Path);
            Assert.True(allocated < AllocationBudget, $"{allocated:N0} bytes allocated.");
        }

        /// <summary>
        /// A count can be honest where it is announced and still be the second of two: the second array
        /// is the one that lies, and the path says so.
        /// </summary>
        [Fact]
        public void ALyingCountInsideAnArrayNamesItsIndex()
        {
            // [[1], [2^28 items...
            CborException exception = Refuse(
                (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<int[][]>(bytes, options)),
                "82" + "8101" + "9A10000000", new CborOptions(), out long allocated);

            Assert.Equal("$[1]", exception.Path);
            Assert.True(allocated < AllocationBudget, $"{allocated:N0} bytes allocated.");
        }

        /// <summary>
        /// A typed array is one byte string, not an array of counted items, so there is no count to
        /// trust - but the length of that byte string is a claim of the same kind, and is checked
        /// against the buffer before anything is allocated for it. Pinned because the two payload
        /// shapes share a converter with the plain arrays above and a change to either must not loosen
        /// the other.
        /// </summary>
        [Theory]
        [InlineData("D84E" + "5A10000000")]                    // tag 78 (int32 LE), 2^28 bytes
        [InlineData("D84E" + "5A7FFFFFFF")]                    // 2^31-1 bytes
        [InlineData("D84E" + "5B8000000000000000")]            // 2^63 bytes
        [InlineData("D84E" + "5A10000000" + "01020304")]       // and four real ones
        public void ALyingTypedArrayLengthIsRefusedWithoutBeingAllocated(string hex)
        {
            CborOptions options = new CborOptions { TypedArrayMode = TypedArrayMode.Read };

            foreach ((string name, ReadAsync read) in new[]
            {
                Shape<int[]>("int[]"),
                Shape<List<int>>("List<int>"),
                Shape<ImmutableArray<int>>("ImmutableArray<int>"),
            })
            {
                CborException exception = Refuse(read, hex, options, out long allocated);

                Assert.Equal("$", exception.Path);
                Assert.True(allocated < AllocationBudget, $"{name} allocated {allocated:N0} bytes.");
            }
        }

        /// <summary>
        /// What was never at risk, and must stay that way: a container that is not given its size up
        /// front, and a string or byte string whose bytes are checked before they are copied. They pin
        /// the bound as a property of the whole reader rather than of three converters.
        /// </summary>
        [Theory]
        [InlineData("HashSet<int>", "9A10000000")]
        [InlineData("SortedSet<int>", "9A10000000")]
        [InlineData("Queue<int>", "9A10000000")]
        [InlineData("Dictionary<int,int>", "BA10000000")]        // a map of 2^28 pairs
        [InlineData("Dictionary<int,int>", "BB8000000000000000")]
        [InlineData("CborObject", "BA10000000")]
        [InlineData("CborObject", "BB8000000000000000")]
        [InlineData("byte[]", "5A10000000")]                     // a byte string of 2^28 bytes
        [InlineData("byte[]", "5B8000000000000000")]
        [InlineData("string", "7A10000000")]
        [InlineData("string", "7B8000000000000000")]
        public void ContainersThatWereNeverPreSizedStayRefused(string shape, string hex)
        {
            ReadAsync read = shape switch
            {
                "HashSet<int>" => Shape<HashSet<int>>(shape).Read,
                "SortedSet<int>" => Shape<SortedSet<int>>(shape).Read,
                "Queue<int>" => Shape<Queue<int>>(shape).Read,
                "Dictionary<int,int>" => Shape<Dictionary<int, int>>(shape).Read,
                "CborObject" => Shape<CborObject>(shape).Read,
                "byte[]" => Shape<byte[]>(shape).Read,
                "string" => Shape<string>(shape).Read,
                _ => throw new ArgumentException(shape),
            };

            CborException exception = Refuse(read, hex, new CborOptions(), out long allocated);

            // Only that there is a path: a container that reads item by item reports the first item it
            // could not read, and one the header refuses reports itself.
            Assert.NotNull(exception.Path);
            Assert.True(allocated < AllocationBudget, $"{shape} allocated {allocated:N0} bytes.");
        }

        /// <summary>
        /// The bound is the bytes that remain and not a smaller number: an array of N items whose items
        /// are one byte each, ending the document, is exactly N bytes and is read.
        /// </summary>
        [Theory]
        [InlineData("80", 0)]                                  // []
        [InlineData("8100", 1)]                                // [0]
        [InlineData("83010203", 3)]                            // [1, 2, 3]
        public void ACountEqualToTheBytesThatRemainIsRead(string hex, int count)
        {
            int[] array = Cbor.Deserialize<int[]>(hex.HexToBytes());
            List<int> list = Cbor.Deserialize<List<int>>(hex.HexToBytes());
            CborArray dom = Cbor.Deserialize<CborArray>(hex.HexToBytes());

            Assert.Equal(count, array.Length);
            Assert.Equal(count, list.Count);
            Assert.Equal(count, dom.Count);
        }

        /// <summary>The same with a count that needs a byte of its own: 24 items, 24 bytes.</summary>
        [Fact]
        public void ACountThatNeedsAFollowingByteIsMeasuredAfterIt()
        {
            string items = string.Concat(Enumerable.Repeat("00", 24));

            Assert.Equal(24, Cbor.Deserialize<int[]>(("9818" + items).HexToBytes()).Length);

            CborException exception = Refuse(
                (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<int[]>(bytes, options)),
                "9819" + items, new CborOptions(), out _);

            Assert.Contains("Unexpected end of buffer", exception.Message);
        }

        /// <summary>
        /// One byte more than the buffer holds is the smallest lie there is, and is refused as the
        /// others are. It also reports as the truncation it is: the same words the reader uses when an
        /// item runs out of bytes.
        /// </summary>
        [Theory]
        [InlineData("81")]                                     // [ then nothing
        [InlineData("8200")]                                   // [0, then nothing
        [InlineData("83" + "0102")]                            // [1, 2, then nothing
        [InlineData("98190000")]                               // 25 items, two bytes
        public void ACountOneBeyondTheBytesThatRemainIsRefused(string hex)
        {
            foreach ((string name, ReadAsync read) in ArrayShapes().Where(s => s.Name is "int[]" or "List<int>" or "CborValue"))
            {
                CborException exception = Refuse(read, hex, new CborOptions(), out _);

                Assert.Equal("$", exception.Path);
                Assert.Contains("Unexpected end of buffer", exception.Message);
            }
        }

        /// <summary>
        /// The bytes that remain are those of the whole buffer and not of the array: a count is
        /// satisfied by the siblings that follow it, so an array in the middle of a document is not held
        /// to the bytes of its own items.
        /// </summary>
        [Fact]
        public void ACountIsMeasuredAgainstTheWholeBufferNotTheEnclosingArray()
        {
            // {"Numbers": [1, 2, 3], "Values": [4]}
            byte[] bytes = ("A2" + "674E756D62657273" + "83010203" + "665661" + "6C756573" + "8104").HexToBytes();

            LengthHolder holder = Cbor.Deserialize<LengthHolder>(bytes);

            Assert.Equal(new[] { 1, 2, 3 }, holder.Numbers);
            Assert.Equal(new[] { 4 }, holder.Values);
        }

        /// <summary>
        /// An indefinite-length array announces no count, so it has nothing to bound, and one that runs
        /// out of bytes fails as it always did.
        /// </summary>
        [Fact]
        public void AnIndefiniteLengthArrayIsUnaffected()
        {
            Assert.Equal(new[] { 1, 2 }, Cbor.Deserialize<int[]>("9F0102FF".HexToBytes()));
            Assert.Equal(new[] { 1, 2 }, Cbor.Deserialize<List<int>>("9F0102FF".HexToBytes()));

            CborException exception = Refuse(
                (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<int[]>(bytes, options)),
                "9F0102", new CborOptions(), out _);

            Assert.Contains("Unexpected end of buffer", exception.Message);
        }

        /// <summary>
        /// An array-format object is read by the same loop as an array and was never pre-sized, so it
        /// is not made worse by the bound: a count the buffer cannot hold is still the one exception
        /// type, with a path.
        /// </summary>
        [Fact]
        public void AnArrayFormatObjectWithALyingCountIsStillACborException()
        {
            CborOptions options = new CborOptions { ObjectFormat = CborObjectFormat.Array };

            CborException exception = Refuse(
                (bytes, o) => Task.FromResult<object>(Cbor.Deserialize<ArrayFormatItem>(bytes, o)),
                "9A7FFFFFFF" + "01", options, out long allocated);

            Assert.Equal("$", exception.Path);
            Assert.True(allocated < AllocationBudget, $"{allocated:N0} bytes allocated.");
        }

        public class ArrayFormatItem
        {
            [CborProperty(0)]
            public int Id { get; set; }
        }

        /// <summary>
        /// Honest data pays nothing for the check and keeps its pre-sized allocation: a million integers
        /// are read back whole by every collection shape, from a span and from a segmented sequence.
        /// </summary>
        [Fact]
        public void AnHonestLargeArrayIsStillRead()
        {
            int[] expected = Enumerable.Range(0, 1_000_000).Select(i => i % 1000).ToArray();
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            Cbor.Serialize(expected, writer);
            byte[] bytes = writer.WrittenSpan.ToArray();

            Assert.Equal(expected, Cbor.Deserialize<int[]>(bytes));
            Assert.Equal(expected, Cbor.Deserialize<List<int>>(bytes));
            Assert.Equal(expected, Cbor.Deserialize<ImmutableArray<int>>(bytes).ToArray());
            Assert.Equal(expected.Length, Cbor.Deserialize<CborArray>(bytes).Count);
            Assert.Equal(expected, Cbor.Deserialize<ImmutableList<int>>(bytes).ToArray());

            // Two segments, split mid-array: the bytes that remain are summed across segments.
            ReadOnlySequence<byte> sequence = Split(bytes, bytes.Length / 2);
            Assert.Equal(expected, Cbor.Deserialize<int[]>(sequence));
            Assert.Equal(expected.Length, Cbor.Deserialize<CborArray>(sequence).Count);
        }

        /// <summary>
        /// The read behind <c>ReadNextItemAsync</c> is speculative: it runs over whatever has arrived, so
        /// the bytes remaining are those of a document that is still being written. An array whose items
        /// have not all arrived is therefore indistinguishable from one that lies, and is handled the
        /// same way it has always been - the read waits for more, and the array is read once it is whole.
        /// </summary>
        [Fact]
        public async Task AnArrayArrivingInPiecesIsWaitedForAndThenReadAsync()
        {
            int[] expected = Enumerable.Range(0, 10_000).Select(i => i % 100).ToArray();
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            Cbor.Serialize(expected, writer);
            byte[] bytes = writer.WrittenSpan.ToArray();

            Pipe pipe = new Pipe();
            Task<int[]> read = Cbor.ReadNextItemAsync<int[]>(pipe.Reader).AsTask();

            int piece = bytes.Length / 7 + 1;

            for (int offset = 0; offset < bytes.Length; offset += piece)
            {
                await pipe.Writer.WriteAsync(bytes.AsMemory(offset, Math.Min(piece, bytes.Length - offset)));

                if (offset + piece < bytes.Length)
                {
                    // Not the last piece: nothing can have been read yet, and the read is still waiting.
                    await Task.Delay(20);
                    Assert.False(read.IsCompleted);
                }
            }

            Assert.Same(read, await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(30))));
            Assert.Equal(expected, await read);
        }

        /// <summary>
        /// And an open pipe cannot tell a lie from an array that has not finished arriving, so it keeps
        /// waiting - it does not allocate for the claim in the meantime - and reports the refusal as
        /// itself once the writer completes and nothing more is coming.
        /// </summary>
        [Fact]
        public async Task ALyingCountOnAnOpenPipeIsReportedOnceThePipeCompletesAsync()
        {
            Pipe pipe = new Pipe();

            await pipe.Writer.WriteAsync("9A10000000".HexToBytes());
            Task<int[]> read = Cbor.ReadNextItemAsync<int[]>(pipe.Reader).AsTask();

            await Task.Delay(100);
            Assert.False(read.IsCompleted);

            await pipe.Writer.CompleteAsync();

            CborException exception = null;

            try
            {
                await read;
            }
            catch (CborException e)
            {
                exception = e;
            }

            Assert.NotNull(exception);
            Assert.Equal("$", exception.Path);
            Assert.Contains("Unexpected end of buffer", exception.Message);
        }

        /// <summary>
        /// The reader itself, below any converter: the size handed to <c>ReadBeginArray</c> is one the
        /// buffer can hold, so a callback that allocates from it is safe to, and one that is handed a
        /// lie is never called.
        /// </summary>
        [Theory]
        [InlineData("83010203", 3, 3, true)]                 // honest
        [InlineData("9F0102FF", -1, 2, true)]                // indefinite: no count
        [InlineData("80", 0, 0, true)]
        [InlineData("9A10000000", 0, 0, false)]              // refused before the callback
        [InlineData("84010203", 0, 0, false)]                // one more than the buffer holds
        [InlineData("9B8000000000000000", 0, 0, false)]
        public void ReadBeginArrayIsOnlyHandedACountTheBufferCanHold(string hex, int size, int items, bool accepted)
        {
            byte[] bytes = hex.HexToBytes();

            AssertReader(new CborReader(bytes), size, items, accepted);
            AssertReader(new CborReader(Split(bytes, Math.Max(1, bytes.Length / 2))), size, items, accepted);
            AssertReader(new CborReader(Helper.Fragmentize(bytes)), size, items, accepted);
        }

        private static void AssertReader(CborReader reader, int size, int items, bool accepted)
        {
            CountingArrayReader callback = new CountingArrayReader();
            CountingArrayReader.Context context = default;
            CborException exception = null;

            try
            {
                reader.ReadArray(callback, ref context);
            }
            catch (CborException e)
            {
                exception = e;
            }

            if (accepted)
            {
                Assert.Null(exception);
                Assert.Equal(1, callback.Begun);
                Assert.Equal(size, callback.Size);
                Assert.Equal(items, callback.Items);
            }
            else
            {
                Assert.NotNull(exception);
                Assert.Equal(0, callback.Begun);
            }
        }

        private sealed class CountingArrayReader : ICborArrayReader<CountingArrayReader.Context>
        {
            public struct Context
            {
            }

            public int Begun { get; private set; }
            public int Size { get; private set; }
            public int Items { get; private set; }

            public void ReadBeginArray(int size, ref Context context)
            {
                Begun++;
                Size = size;
            }

            public void ReadArrayItem(ref CborReader reader, ref Context context)
            {
                reader.ReadInt32();
                Items++;
            }
        }

        /// <summary>
        /// Runs one read to completion on this thread and returns the exception it ended with, with the
        /// bytes this thread allocated getting there. The type is warmed first with a read that fails
        /// the same way on a count of two, so converter construction is not charged to the lie.
        /// </summary>
#pragma warning disable VSTHRD002 // Every read here is complete before it returns, and has to be: its allocation is read off this thread.
        private static CborException Refuse(ReadAsync read, string hex, CborOptions options, out long allocated)
        {
            byte[] bytes = hex.HexToBytes();

            try
            {
                read("8201".HexToBytes(), options).GetAwaiter().GetResult();
            }
            catch (CborException)
            {
            }

            long before = GC.GetAllocatedBytesForCurrentThread();

            try
            {
                Task<object> task = read(bytes, options);

                Assert.True(task.IsCompleted, "The read did not complete synchronously, so its allocation cannot be measured.");
                task.GetAwaiter().GetResult();
            }
            catch (CborException exception)
            {
                allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                return exception;
            }

            throw new XunitException($"Reading {hex} did not fail.");
        }
#pragma warning restore VSTHRD002

        private static (string Name, ReadAsync Read) Shape<T>(string name)
        {
            return (name, (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<T>(bytes, options)));
        }

        private static ReadOnlySequence<byte> Split(byte[] bytes, int at)
        {
            Segment first = new Segment(bytes.AsMemory(0, at), 0);
            Segment last = first.Append(bytes.AsMemory(at));

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
            {
                Memory = memory;
                RunningIndex = runningIndex;
            }

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                Segment next = new Segment(memory, RunningIndex + Memory.Length);
                Next = next;
                return next;
            }
        }

        /// <summary>
        /// The nine doors of <see cref="Cbor"/>'s reading half that take their bytes from a buffer, a
        /// stream or a pipe. The synchronous ones are wrapped, and the asynchronous ones are completed
        /// before they return because everything they read from is already in memory.
        /// </summary>
        private static IEnumerable<object[]> EntryPointsFor<T>(string shape)
        {
            yield return Row(shape, "Deserialize(ReadOnlySpan)",
                (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<T>(bytes.AsSpan(), options)));

            yield return Row(shape, "Deserialize(ReadOnlySequence)",
                (bytes, options) => Task.FromResult<object>(Cbor.Deserialize<T>(Split(bytes, Math.Max(1, bytes.Length / 2)), options)));

            yield return Row(shape, "DeserializeAsync(Stream)",
                async (bytes, options) => await Cbor.DeserializeAsync<T>(new MemoryStream(bytes), options));

            yield return Row(shape, "DeserializeAsync(PipeReader)",
                async (bytes, options) => await Cbor.DeserializeAsync<T>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options));

            yield return Row(shape, "DeserializeMultiple(ReadOnlySpan)",
                (bytes, options) => Task.FromResult<object>(Cbor.DeserializeMultiple<T>(bytes.AsSpan(), options)));

            yield return Row(shape, "DeserializeMultiple(ReadOnlySequence)",
                (bytes, options) => Task.FromResult<object>(Cbor.DeserializeMultiple<T>(new ReadOnlySequence<byte>(bytes), options)));

            yield return Row(shape, "DeserializeMultipleAsync(Stream)",
                async (bytes, options) => await Cbor.DeserializeMultipleAsync<T>(new MemoryStream(bytes), options));

            yield return Row(shape, "DeserializeMultipleAsync(PipeReader)",
                async (bytes, options) => await Cbor.DeserializeMultipleAsync<T>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options));

            yield return Row(shape, "ReadNextItemAsync(PipeReader)",
                async (bytes, options) => await Cbor.ReadNextItemAsync<T>(
                    PipeReader.Create(new ReadOnlySequence<byte>(bytes)), options));
        }

        private static object[] Row(string shape, string door, ReadAsync read)
        {
            return new object[] { $"{door} as {shape}", read };
        }
    }
}
