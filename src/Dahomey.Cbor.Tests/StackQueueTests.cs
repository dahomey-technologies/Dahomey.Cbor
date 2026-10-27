using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A stack or a queue is written as an array in enumeration order, top first for a stack, and
    /// reads back in the same order: what was written, and in particular not a reversed stack.
    /// </summary>
    public class StackQueueTests
    {
        public class Holder
        {
            public Stack<int> S { get; set; }
            public Queue<int> Q { get; set; }
            public ConcurrentStack<int> CS { get; set; }
            public ConcurrentQueue<int> CQ { get; set; }
            public ImmutableStack<int> IS { get; set; }
            public ImmutableQueue<int> IQ { get; set; }
        }

        [Fact]
        public void AStackIsWrittenTopFirstAndReadsBackUnchanged()
        {
            Stack<int> stack = new Stack<int>(new[] { 1, 2, 3 });   // 3 on top

            Assert.Equal("83030201", Helper.Write(stack));
            Assert.Equal(new[] { 3, 2, 1 }, Helper.Read<Stack<int>>("83030201").ToArray());
            Assert.Equal(3, Helper.Read<Stack<int>>("83030201").Peek());
        }

        [Fact]
        public void AQueueIsWrittenFrontFirstAndReadsBackUnchanged()
        {
            Queue<int> queue = new Queue<int>(new[] { 1, 2, 3 });   // 1 in front

            Assert.Equal("83010203", Helper.Write(queue));
            Assert.Equal(1, Helper.Read<Queue<int>>("83010203").Peek());
        }

        [Fact]
        public void EveryFormRoundTripsAsAMember()
        {
            Holder value = new Holder
            {
                S = new Stack<int>(new[] { 1, 2, 3 }),
                Q = new Queue<int>(new[] { 1, 2, 3 }),
                CS = new ConcurrentStack<int>(new[] { 1, 2, 3 }),
                CQ = new ConcurrentQueue<int>(new[] { 1, 2, 3 }),
                IS = ImmutableStack.Create(1, 2, 3),
                IQ = ImmutableQueue.Create(1, 2, 3),
            };

            Holder read = Cbor.Deserialize<Holder>(Helper.Write(value).HexToBytes());

            Assert.Equal(value.S, read.S);
            Assert.Equal(value.Q, read.Q);
            Assert.Equal(value.CS, read.CS);
            Assert.Equal(value.CQ, read.CQ);
            Assert.Equal(value.IS, read.IS);
            Assert.Equal(value.IQ, read.IQ);
        }

        [Fact]
        public void NullAndEmptyRoundTrip()
        {
            Assert.Null(Helper.Read<ImmutableStack<int>>("F6"));
            Assert.Equal("F6", Helper.Write<Queue<int>>(null));
            Assert.True(Helper.Read<ImmutableQueue<int>>("80").IsEmpty);
            Assert.Equal("80", Helper.Write(ImmutableStack<int>.Empty));
        }
    }
}
