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
        }

        public struct WriterContext
        {
            public int count;
            public IEnumerator<TI> enumerator;
            public LengthMode lengthMode;
        }

        private readonly ICborConverter<TI> _itemConverter;

        public AbstractCollectionConverter(SerializationRegistry registry)
        {
            _itemConverter = registry.ConverterRegistry.Lookup<TI>();
        }

        protected abstract ICollection<TI> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(ICollection<TI> tempCollection);

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadArray(this, ref context);

            return InstantiateCollection(context.collection);
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
                    ? lengthMode : writer.Options.ArrayLengthMode
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
            TI item = _itemConverter.Read(ref reader);
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
                _itemConverter.Write(ref writer, context.enumerator.Current);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
