using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Serialization.Converters
{
    public abstract class AbstractCollectionConverter<TC, TI> :
        CborConverterBase<TC>,
        ICborArrayReader<AbstractCollectionConverter<TC, TI>.ReaderContext>,
        ICborArrayWriter<AbstractCollectionConverter<TC, TI>.WriterContext>
        where TC : IEnumerable<TI>
    {
        public struct ReaderContext
        {
            public ICollection<TI> collection;

            /// <summary>
            /// Index of the item currently being read, or -1 before the first one.
            /// </summary>
            /// <remarks>
            /// Only read when an item has thrown, to name the position in
            /// <see cref="CborException.Path"/>. It is incremented before the item is read rather than
            /// after, so that the failing index is the one reported.
            /// </remarks>
            public int index;
        }

        public struct WriterContext
        {
            public int count;
            public IEnumerator<TI> enumerator;
            public LengthMode lengthMode;
        }

        private readonly CborOptions _options;

        public AbstractCollectionConverter(CborOptions options)
        {
            _options = options;
        }

        private ICborConverter<TI> ItemConverter => field ??= _options.Registry.ConverterRegistry.Lookup<TI>();

        protected abstract ICollection<TI> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(ICollection<TI> tempCollection);

        public override TC Read(ref CborReader reader)
        {
            if ((_options.TypedArrayMode & TypedArrayMode.Read) != 0 && reader.IsSemanticTag())
            {
                CborReaderBookmark bookmark = reader.GetBookmark();

                if (reader.TryReadSemanticTag(out ulong tag) && TypedArrayTags.IsTypedArrayTag(tag))
                {
                    try
                    {
                        return ReadTypedArray(ref reader, tag);
                    }
                    catch (CborException exception)
                    {
                        // A typed array is decoded whole rather than item by item, so a failure is a
                        // property of the array, not of a position in it. The collection itself is
                        // still a position worth reporting.
                        exception.MarkPathKnown();
                        throw;
                    }
                }

                // Some other tag, which CBOR says to ignore. Hand it back so the ReadNull below skips
                // exactly one, as it did before typed arrays existed.
                reader.ReturnToBookmark(bookmark);
            }

            if (reader.ReadNull())
            {
                return default!;
            }

            // Before the first item, so that a failure on the array header itself - the common case
            // of a document that is not an array at all - is not attributed to item 0.
            ReaderContext context = new ReaderContext { index = -1 };

            try
            {
                reader.ReadArray(this, ref context);
            }
            catch (CborException exception)
            {
                if (context.index >= 0)
                {
                    exception.PrependPathIndex(context.index);
                }
                else
                {
                    exception.MarkPathKnown();
                }

                throw;
            }

            return InstantiateCollection(context.collection);
        }

        /// <summary>
        /// Fills this collection from an RFC 8746 typed array, so that a document written from a
        /// <c>TI[]</c> member reads back into any of the collection shapes that are interchangeable
        /// with it - <c>List&lt;TI&gt;</c>, <c>IList&lt;TI&gt;</c>, <c>HashSet&lt;TI&gt;</c>,
        /// <c>ImmutableArray&lt;TI&gt;</c> and the rest.
        /// </summary>
        /// <remarks>
        /// The decoding itself belongs to the converter for <c>TI[]</c>: this class has no
        /// <c>unmanaged</c> constraint on <c>TI</c> and so cannot do the <see cref="System.Runtime.InteropServices.MemoryMarshal"/>
        /// cast, but it can ask the registry for that converter and use it through
        /// <see cref="ITypedArrayReader{TI}"/>. The lookup happens here rather than in the constructor
        /// so that it costs nothing until a typed array actually arrives, and so that it cannot take
        /// part in a converter construction cycle.
        /// </remarks>
        private TC ReadTypedArray(ref CborReader reader, ulong tag)
        {
            if (_options.Registry.ConverterRegistry.Lookup<TI[]>() is not ITypedArrayReader<TI> typedArrayReader)
            {
                throw new CborException(
                    $"Cannot read a typed array tagged {tag} ({TypedArrayTags.DescribeTag(tag)}) into {typeof(TC)}.");
            }

            TI[] items = typedArrayReader.ReadTypedArray(ref reader, tag);
            ICollection<TI> collection = InstantiateTempCollection();

            if (collection is List<TI> list)
            {
                list.Capacity = items.Length;
            }

            foreach (TI item in items)
            {
                collection.Add(item);
            }

            return InstantiateCollection(collection);
        }

        public override void Write(ref CborWriter writer, TC value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            WriterContext context = new WriterContext
            {
                count = value.Count(),
                enumerator = value.GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.ArrayLengthMode
            };

            writer.WriteArray(this, ref context);
        }

        public void ReadBeginArray(int size, ref ReaderContext context)
        {
            context.collection = InstantiateTempCollection();

            if (size != -1 && context.collection is List<TI> list)
            {
                list.Capacity = size;
            }
        }

        public void ReadArrayItem(ref CborReader reader, ref ReaderContext context)
        {
            context.index++;
            TI item = ItemConverter.Read(ref reader);
            context.collection.Add(item);
        }

        public int GetArraySize(ref WriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.count;
        }

        public bool WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            if (context.enumerator.MoveNext())
            {
                ItemConverter.Write(ref writer, context.enumerator.Current);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
