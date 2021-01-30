using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ArrayConverter<TI> :
        CborConverterBase<TI[]?>
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
            public LengthMode lengthMode;
        }

        private readonly CborOptions _options;
        private readonly ICborConverter<TI> _itemConverter;

        public ArrayConverter(CborOptions options)
        {
            _options = options;
            _itemConverter = options.Registry.ConverterRegistry.Lookup<TI>();
        }

        public override TI[]? Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return null;
            }

            ReaderContext context = new ReaderContext();

            reader.ReadBeginArray();

            int size = reader.ReadSize();

            ReadBeginArray(size, ref context);

            while (size > 0 || size < 0 && reader.GetCurrentDataItemType() != CborDataItemType.Break)
            {
                ReadArrayItem(ref reader, ref context);
                size--;
            }

            reader.ReadEndArray();

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

            int size = GetArraySize(ref context);
            writer.WriteBeginArray(size);
            while (WriteArrayItem(ref writer, ref context)) ;
            writer.WriteEndArray(size);
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
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.array.Length;
        }

        public bool WriteArrayItem(ref CborWriter writer, ref WriterContext context)
        {
            _itemConverter.Write(ref writer, context.array[context.index++]);
            return context.index < context.array.Length;
        }
    }
}
