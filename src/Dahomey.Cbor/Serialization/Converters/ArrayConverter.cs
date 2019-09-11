using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ArrayConverter<TI> :
        CborConverterBase<TI[]>, 
        ICborArrayReader<ArrayConverter<TI>.ReaderContext>,
        ICborArrayWriter<ArrayConverter<TI>.WriterContext>
    {
        public struct ReaderContext
        {
            public TI[] array;
            public List<TI> list;
            public int index;
        }

        public struct WriterContext
        {
            public TI[] array;
            public int index;
        }

        private readonly ICborConverter<TI> _itemConverter;

        public ArrayConverter(SerializationRegistry registry)
        {
            _itemConverter = registry.ConverterRegistry.Lookup<TI>();
        }

        public override TI[] Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadArray(this, ref context);

            if (context.array != null)
            {
                return context.array;
            }
            else
            {
                return context.list.ToArray();
            }
        }

        public override void Write(ref CborWriter writer, TI[] value)
        {
            WriterContext context = new WriterContext
            {
                array = value
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
            TI item = _itemConverter.Read(ref reader);

            if (context.array != null)
            {
                context.array[context.index++] = item;
            }
            else
            {
                context.list.Add(item);
            }
        }

        public int GetArraySize(ref WriterContext context)
        {
            return context.array?.Length ?? 0;
        }

        public void WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            _itemConverter.Write(ref writer, context.array[context.index++]);
        }
    }
}
