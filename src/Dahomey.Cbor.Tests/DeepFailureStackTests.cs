using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Providers;
using Dahomey.Cbor.Tests.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// A read that fails deep inside a nested document reports the failure on a thread with the stack
    /// an ordinary request thread has, rather than ending the process.
    /// </summary>
    /// <remarks>
    /// Each converter on the way up adds its segment to <see cref="CborException.Path"/>. A <c>catch</c>
    /// that rethrows runs on top of the frames that threw and starts another dispatch on top of itself,
    /// so a path built that way costs about 16 KB of stack per level: a text string where the leaf of a
    /// class nested 32 deep wants an integer needs more than 1 MB, and so does the 65-level payload that
    /// is supposed to answer "nesting depth exceeded". A <see cref="StackOverflowException"/> cannot be
    /// caught, so the process ends. The converters add their segment from an exception filter instead,
    /// which runs before anything is unwound and leaves nothing waiting, so a failure costs about what a
    /// valid read of the same depth does.
    /// <para>
    /// These run on a thread of their own with a 1 MB stack, the size of a Windows thread and below what
    /// most hosts give a request. A test that fails here does not report a failure: the test host exits.
    /// </para>
    /// </remarks>
    public class DeepFailureStackTests
    {
        private const int StackKilobytes = 1024;

        private class Node
        {
            public Node Child { get; set; }
            public int Leaf { get; set; }
        }

        private class ListNode
        {
            public List<ListNode> Items { get; set; }
            public int Leaf { get; set; }
        }

        private class ArrayNode
        {
            public ArrayNode[] Items { get; set; }
            public int Leaf { get; set; }
        }

        private class ImmutableNode
        {
            public ImmutableList<ImmutableNode> Items { get; set; }
            public int Leaf { get; set; }
        }

        private class MapNode
        {
            public Dictionary<string, MapNode> Map { get; set; }
            public int Leaf { get; set; }
        }

        private class TupleNode
        {
            public (int, TupleNode) T { get; set; }
            public int Leaf { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyedNode
        {
            [CborProperty(1)]
            public IntKeyedNode Child { get; set; }

            [CborProperty(2)]
            public int Leaf { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        private class PositionalNode
        {
            [CborProperty(0)]
            public PositionalNode Child { get; set; }

            [CborProperty(1)]
            public int Leaf { get; set; }
        }

        /// <summary>A shape of nesting: how a document of it is built, and what its path calls each level.</summary>
        private sealed class Shape
        {
            public Func<int, byte[], byte[]> Build;
            public string Segment;
            public Func<byte[], CborOptions, object> Read;

            /// <summary>How much of <see cref="CborOptions.MaxDepth"/> each level uses.</summary>
            public int DepthPerLevel;
        }

        /// <summary>
        /// A document of <paramref name="levels"/> objects, each holding the next under <paramref name="key"/>
        /// after <paramref name="wrapper"/> - nothing for an object, <c>81</c> for a one-item array,
        /// <c>A1 61 6B</c> for the map <c>{"k": …}</c> - and under them <c>{"Leaf": …}</c>. Built rather
        /// than written out, since the fixture is a hundred bytes of the same eight repeated.
        /// </summary>
        private static Func<int, byte[], byte[]> Keyed(string key, params byte[] wrapper)
        {
            return (levels, leaf) =>
            {
                List<byte> bytes = new List<byte>();

                for (int i = 0; i < levels; i++)
                {
                    bytes.Add(0xA1);                                      // map(1)
                    bytes.Add((byte)(0x60 + key.Length));                 // text(n)
                    bytes.AddRange(Encoding.ASCII.GetBytes(key));
                    bytes.AddRange(wrapper);
                }

                bytes.Add(0xA1);                                          // map(1)
                bytes.AddRange("644C656166".HexToBytes());                // "Leaf"
                bytes.AddRange(leaf);

                return bytes.ToArray();
            };
        }

        private static readonly Shape ObjectShape = new Shape
        {
            Build = Keyed("Child"), Segment = ".Child", DepthPerLevel = 1,
            Read = (b, o) => Cbor.Deserialize<Node>(b, o),
        };

        private static readonly Shape ListShape = new Shape
        {
            Build = Keyed("Items", 0x81), Segment = ".Items[0]", DepthPerLevel = 2,
            Read = (b, o) => Cbor.Deserialize<ListNode>(b, o),
        };

        private static readonly Shape ArrayShape = new Shape
        {
            Build = Keyed("Items", 0x81), Segment = ".Items[0]", DepthPerLevel = 2,
            Read = (b, o) => Cbor.Deserialize<ArrayNode>(b, o),
        };

        private static readonly Shape ImmutableShape = new Shape
        {
            Build = Keyed("Items", 0x81), Segment = ".Items[0]", DepthPerLevel = 2,
            Read = (b, o) => Cbor.Deserialize<ImmutableNode>(b, o),
        };

        private static readonly Shape MapShape = new Shape
        {
            Build = Keyed("Map", 0xA1, 0x61, 0x6B), Segment = ".Map.k", DepthPerLevel = 2,
            Read = (b, o) => Cbor.Deserialize<MapNode>(b, o),
        };

        private static readonly Shape TupleShape = new Shape
        {
            // The array of a tuple is not counted against MaxDepth, only the object holding it is.
            Build = Keyed("T", 0x82, 0x01), Segment = ".T[1]", DepthPerLevel = 1,
            Read = (b, o) => Cbor.Deserialize<TupleNode>(b, o),
        };

        private static readonly Shape IntKeyShape = new Shape
        {
            // {1: {1: … {2: leaf}}}
            Build = (levels, leaf) => Enumerable.Repeat(new byte[] { 0xA1, 0x01 }, levels)
                .SelectMany(b => b).Concat(new byte[] { 0xA1, 0x02 }).Concat(leaf).ToArray(),
            Segment = ".Child", DepthPerLevel = 1,
            Read = (b, o) => Cbor.Deserialize<IntKeyedNode>(b, o),
        };

        private static readonly Shape PositionalShape = new Shape
        {
            // [[… [null, leaf] …, 0], 0]: each level is [child, 0], the innermost [null, leaf]
            Build = (levels, leaf) => Enumerable.Repeat((byte)0x82, levels)
                .Concat(new byte[] { 0x82, 0xF6 }).Concat(leaf)
                .Concat(Enumerable.Repeat((byte)0x00, levels)).ToArray(),
            Segment = ".Child", DepthPerLevel = 1,
            Read = (b, o) => Cbor.Deserialize<PositionalNode>(b, o),
        };

        public static IEnumerable<object[]> Shapes()
        {
            yield return new object[] { nameof(ObjectShape) };
            yield return new object[] { nameof(IntKeyShape) };
            yield return new object[] { nameof(PositionalShape) };
            yield return new object[] { nameof(ListShape) };
            yield return new object[] { nameof(ArrayShape) };
            yield return new object[] { nameof(ImmutableShape) };
            yield return new object[] { nameof(MapShape) };
            yield return new object[] { nameof(TupleShape) };
        }

        private static Shape ShapeNamed(string name)
        {
            switch (name)
            {
                case nameof(ObjectShape): return ObjectShape;
                case nameof(IntKeyShape): return IntKeyShape;
                case nameof(PositionalShape): return PositionalShape;
                case nameof(ListShape): return ListShape;
                case nameof(ArrayShape): return ArrayShape;
                case nameof(ImmutableShape): return ImmutableShape;
                case nameof(MapShape): return MapShape;
                case nameof(TupleShape): return TupleShape;
                default: throw new ArgumentException(name);
            }
        }

        private static readonly byte[] TextWhereAnIntegerIsWanted = { 0x61, 0x78 };   // "x"

        private static string ExpectedPath(Shape shape, int levels)
        {
            StringBuilder path = new StringBuilder("$");

            for (int i = 0; i < levels; i++)
            {
                path.Append(shape.Segment);
            }

            return path.ToString();
        }

        /// <summary>Runs on a thread with a stack of <see cref="StackKilobytes"/>, rethrowing what it throws.</summary>
        private static CborException FailOnASmallStack(Shape shape, byte[] document, CborOptions options = null)
        {
            Exception outcome = null;

            Thread thread = new Thread(
                () =>
                {
                    try
                    {
                        shape.Read(document, options);
                        outcome = new InvalidOperationException("The read was expected to fail and did not.");
                    }
                    catch (Exception exception)
                    {
                        outcome = exception;
                    }
                },
                StackKilobytes * 1024);

            thread.Start();
            thread.Join();

            CborException failure = outcome as CborException;

            if (failure == null)
            {
                ExceptionDispatchInfo.Capture(outcome).Throw();
            }

            return failure;
        }

        /// <summary>
        /// The failure from the report: a class nested 32 deep, and a text string where its innermost
        /// member wants an integer.
        /// </summary>
        [Fact]
        public void AClassNested32DeepFailsAtItsLeafWithTheFullPath()
        {
            CborException exception = FailOnASmallStack(ObjectShape, ObjectShape.Build(32, TextWhereAnIntegerIsWanted));

            Assert.Equal(ExpectedPath(ObjectShape, 32) + ".Leaf", exception.Path);
            Assert.EndsWith($"Failed to deserialize from \"{exception.Path}\".", exception.Message);
        }

        /// <summary>
        /// One level more than <see cref="CborOptions.MaxDepth"/> allows, which has to answer with
        /// "nesting depth exceeded" rather than end the process.
        /// </summary>
        [Fact]
        public void AnObjectOneLevelPastMaxDepthIsRefusedAsTooDeep()
        {
            int maxDepth = CborOptions.Default.MaxDepth;
            CborException exception = FailOnASmallStack(
                ObjectShape, ObjectShape.Build(maxDepth, TextWhereAnIntegerIsWanted));

            Assert.Contains("nesting depth exceeded", exception.Message);
            Assert.StartsWith("$.Child", exception.Path);
        }

        /// <summary>
        /// Two levels of depth per level of the model - the object and the collection it holds - so
        /// 31 levels is the deepest the default allows, and the leaf is what fails. A tuple counts one.
        /// </summary>
        [Theory]
        [MemberData(nameof(Shapes))]
        public void ACollectionPerLevelFailsAtItsLeafWithTheFullPath(string shapeName)
        {
            Shape shape = ShapeNamed(shapeName);
            int levels = (CborOptions.Default.MaxDepth - 1) / shape.DepthPerLevel;

            CborException exception = FailOnASmallStack(shape, shape.Build(levels, TextWhereAnIntegerIsWanted));

            Assert.Equal(ExpectedPath(shape, levels) + ".Leaf", exception.Path);
        }

        [Theory]
        [MemberData(nameof(Shapes))]
        public void ACollectionPerLevelOnePastMaxDepthIsRefusedAsTooDeep(string shapeName)
        {
            Shape shape = ShapeNamed(shapeName);
            int levels = CborOptions.Default.MaxDepth / shape.DepthPerLevel;

            CborException exception = FailOnASmallStack(shape, shape.Build(levels, TextWhereAnIntegerIsWanted));

            Assert.Contains("nesting depth exceeded", exception.Message);
        }

        /// <summary>
        /// An integer converter that fails a text string, but first spends the stack down to the last
        /// stretch the runtime reserves for a function to run in.
        /// </summary>
        /// <remarks>
        /// The depth of a failure is not the only thing the path costs; what the path costs <i>after</i>
        /// the throw is what differs between a filter and a rethrow, and that is not visible while there
        /// is stack to spare. Throwing from the bottom of the stack makes it visible whatever the shape:
        /// a filter needs the one dispatch, wherever the throw is, and each <c>catch</c> that rethrows
        /// needs another on top of it.
        /// </remarks>
        private class FailWithTheStackSpentConverter : CborConverterBase<int>
        {
            public override int Read(ref CborReader reader)
            {
                if (reader.GetCurrentDataItemType() == CborDataItemType.String)
                {
                    return SpendTheStack(0);
                }

                return reader.ReadInt32();
            }

            public override void Write(ref CborWriter writer, int value)
            {
                writer.WriteInt32(value);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static int SpendTheStack(int depth)
            {
                Span<byte> padding = stackalloc byte[2048];
                padding[0] = (byte)depth;

                if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
                {
                    throw new CborException("The stack is down to its last stretch.");
                }

                return SpendTheStack(depth + 1) + padding[0];
            }
        }

        private class FailWithTheStackSpentProvider : ICborConverterProvider
        {
            public ICborConverter GetConverter(Type type, CborOptions options)
            {
                return type == typeof(int) ? new FailWithTheStackSpentConverter() : null;
            }
        }

        /// <summary>
        /// Every converter that adds a segment does it without another dispatch: throwing with the
        /// stack nearly spent still reaches the caller with the whole path, however many levels
        /// there are to add it. The runtime keeps 128 KB back for this check on a 64-bit process; one
        /// <c>catch</c> that rethrows costs about 16 KB of it, and a document 31 levels deep has 31
        /// of them to pass.
        /// </summary>
        [Theory]
        [MemberData(nameof(Shapes))]
        public void APathIsBuiltInTheStackThatIsLeftAfterTheThrow(string shapeName)
        {
            Shape shape = ShapeNamed(shapeName);
            int levels = (CborOptions.Default.MaxDepth - 1) / shape.DepthPerLevel;

            // The registry has already built the converter for int by the time the options exist, so a
            // provider is registered and the cache cleared rather than the converter registered.
            CborOptions options = new CborOptions();
            options.Registry.ConverterRegistry.RegisterConverterProvider(new FailWithTheStackSpentProvider());
            options.Registry.ConverterRegistry.Reset();

            CborException exception = FailOnASmallStack(shape, shape.Build(levels, TextWhereAnIntegerIsWanted), options);

            Assert.StartsWith("The stack is down to its last stretch.", exception.Message);
            Assert.Equal(ExpectedPath(shape, levels) + ".Leaf", exception.Path);
        }

        /// <summary>
        /// The control: the same stack reads the deepest valid document, so the failures above are
        /// not simply a stack too small for the data.
        /// </summary>
        [Fact]
        public void TheSameStackReadsAValidDocumentNestedToMaxDepth()
        {
            int levels = CborOptions.Default.MaxDepth - 1;
            byte[] document = ObjectShape.Build(levels, new byte[] { 0x05 });
            Node node = null;
            Exception failure = null;

            Thread thread = new Thread(
                () =>
                {
                    try
                    {
                        node = Cbor.Deserialize<Node>(document);
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                },
                StackKilobytes * 1024);

            thread.Start();
            thread.Join();

            Assert.Null(failure);

            for (int i = 0; i < levels; i++)
            {
                node = node.Child;
            }

            Assert.Equal(5, node.Leaf);
        }
    }
}
