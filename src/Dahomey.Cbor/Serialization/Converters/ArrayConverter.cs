using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ArrayConverter<TI> :
        CborConverterBase<TI[]?>, 
        ICborArrayReader<ArrayConverter<TI>.ReaderContext>,
        ICborArrayWriter<ArrayConverter<TI>.WriterContext>
    {
        public struct ReaderContext
        {
            public TI[] array;
            public List<TI> list;

            /// <summary>
            /// Index of the item currently being read, or -1 before the first one. Doubles as the
            /// write position on the definite-length branch, where the array is allocated up front.
            /// </summary>
            /// <remarks>
            /// Read on failure to name the position in <see cref="CborException.Path"/>. It is
            /// advanced before the item is read rather than as a side effect of storing it, so that
            /// an item that throws has already claimed its own index and the indefinite-length branch
            /// counts too.
            /// </remarks>
            public int index;
        }

        public struct WriterContext
        {
            public TI[] array;
            public int index;
            public LengthMode lengthMode;
        }

        protected readonly CborOptions _options;

        public ArrayConverter(CborOptions options)
        {
            _options = options;
        }

        private ICborConverter<TI> ItemConverter => field ??= _options.Registry.ConverterRegistry.Lookup<TI>();

        public override TI[]? Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            // Before the first item, so that a failure on the array header itself - the common case of
            // a document that is not an array at all - is not attributed to item 0.
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

            if (context.array != null)
            {
                return context.array;
            }
            else
            {
                return context.list.ToArray();
            }
        }

        public override void Write(ref CborWriter writer, TI[]? value, LengthMode lengthMode)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            WriterContext context = new WriterContext
            {
                array = value,
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.ArrayLengthMode
            };

            writer.WriteArray(this, ref context);
        }

        public void ReadBeginArray(int size, ref ReaderContext context)
        {
            if (size != -1)
            {
                context.array = new TI[size];
            }
            else
            {
                context.list = new List<TI>();
            }
        }

        public void ReadArrayItem(ref CborReader reader, ref ReaderContext context)
        {
            int index = ++context.index;
            TI item = ItemConverter.Read(ref reader);

            if (context.array != null)
            {
                context.array[index] = item;
            }
            else
            {
                context.list.Add(item);
            }
        }

        public int GetArraySize(ref WriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.array.Length;
        }

        public bool WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            if (context.array.Length == 0)
            {
                return false;
            }

            ItemConverter.Write(ref writer, context.array[context.index++]);
            return context.index < context.array.Length;
        }
    }
}
