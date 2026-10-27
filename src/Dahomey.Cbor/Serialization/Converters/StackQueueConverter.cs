using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes <see cref="Stack{T}"/>, <see cref="Queue{T}"/>, their concurrent and immutable
    /// forms as an array, in enumeration order: front to back for a queue, top to bottom for a stack.
    /// </summary>
    /// <remarks>
    /// None of them implements <see cref="ICollection{T}"/>, so they used to fall through to the
    /// object mapper, which wrote their public properties and read back an empty collection. A stack
    /// is rebuilt by pushing from the bottom, so it reads back in the order it was written, where
    /// <c>System.Text.Json</c> pushes in document order and reverses it.
    /// </remarks>
    public class StackQueueConverter<TC, TI> : AbstractCollectionConverter<TC, TI>
        where TC : class, IEnumerable<TI>
    {
        private readonly CborOptions _options;
        private readonly Func<List<TI>, TC> _create;

        public StackQueueConverter(CborOptions options)
            : base(options)
        {
            _options = options;
            _create = GetFactory(typeof(TC).GetGenericTypeDefinition())
                ?? throw new CborException($"{typeof(TC)} is not a stack or a queue");
        }

        private static Func<List<TI>, TC>? GetFactory(Type definition)
        {
            if (definition == typeof(Queue<>)) return items => (TC)(object)new Queue<TI>(items);
            if (definition == typeof(ConcurrentQueue<>)) return items => (TC)(object)new ConcurrentQueue<TI>(items);
            if (definition == typeof(ImmutableQueue<>)) return items => (TC)(object)ImmutableQueue.CreateRange(items);
            if (definition == typeof(Stack<>)) return items => (TC)(object)new Stack<TI>(BottomFirst(items));
            if (definition == typeof(ConcurrentStack<>)) return items => (TC)(object)new ConcurrentStack<TI>(BottomFirst(items));
            if (definition == typeof(ImmutableStack<>)) return items => (TC)(object)ImmutableStack.CreateRange(BottomFirst(items));
            return null;
        }

        private static List<TI> BottomFirst(List<TI> items)
        {
            items.Reverse();
            return items;
        }

        public override void Write(ref CborWriter writer, TC value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // Counted and enumerated from one snapshot: a concurrent collection changed between the
            // two would otherwise write an array whose header disagrees with its items.
            TI[] items = value.ToArray();

            WriterContext context = new WriterContext
            {
                count = items.Length,
                enumerator = ((IEnumerable<TI>)items).GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default ? lengthMode : _options.ArrayLengthMode
            };

            writer.WriteArray(this, ref context);
        }

        protected override ICollection<TI> InstantiateTempCollection()
        {
            return new List<TI>();
        }

        protected override TC InstantiateCollection(ICollection<TI> tempCollection)
        {
            return _create((List<TI>)tempCollection);
        }
    }
}
