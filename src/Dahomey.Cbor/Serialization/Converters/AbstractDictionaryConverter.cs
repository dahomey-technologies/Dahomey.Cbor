using System;
using System.Collections.Generic;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public abstract class AbstractDictionaryConverter<TC, TK, TV> :
        CborConverterBase<TC>, 
        ICborMapReader<AbstractDictionaryConverter<TC, TK, TV>.ReaderContext>,
        ICborMapWriter<AbstractDictionaryConverter<TC, TK, TV>.WriterContext>
        where TC : notnull, IDictionary<TK, TV>
        where TK : notnull
    {
        public struct ReaderContext
        {
            public IDictionary<TK, TV> dict;
        }

        public struct WriterContext
        {
            public int count;
            public IEnumerator<KeyValuePair<TK, TV>> enumerator;
            public LengthMode lengthMode;
        }

        private readonly CborOptions _options;

        protected abstract IDictionary<TK, TV> InstantiateTempCollection();
        protected abstract TC InstantiateCollection(IDictionary<TK, TV> tempCollection);

        public AbstractDictionaryConverter(CborOptions options)
        {
            _options = options;
        }

        private ICborConverter<TK> KeyConverter => field ??= _options.Registry.ConverterRegistry.Lookup<TK>();
        private ICborConverter<TV> ValueConverter => field ??= _options.Registry.ConverterRegistry.Lookup<TV>();

        public override TC Read(ref CborReader reader)
        {
            if (reader.ReadNull())
            {
                return default!;
            }

            ReaderContext context = new ReaderContext();
            reader.ReadMap(this, ref context);
            return InstantiateCollection(context.dict);
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
                count = value.Count,
                enumerator = _options.Deterministic
                    ? SortedEntries(value).GetEnumerator()
                    : value.GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.MapLengthMode
            };

            writer.WriteMap(this, ref context);
        }

        // The key set is only known at write time (unlike fixed object members, which are sorted once
        // at mapping build time in ObjectConverter), so this materialises and sorts on every write.
        // GetMapSize/WriteMapItem are untouched: they only ever pump context.enumerator, so handing
        // them a sorted list's enumerator instead of the dictionary's is enough.
        private static List<KeyValuePair<TK, TV>> SortedEntries(IDictionary<TK, TV> dictionary)
        {
            List<KeyValuePair<TK, TV>> entries = new List<KeyValuePair<TK, TV>>(dictionary);

            try
            {
                entries.Sort(CompareEntriesForDeterministicOrder);
            }
            catch (InvalidOperationException ex) when (ex.InnerException is CborException cborException)
            {
                // List<T>.Sort wraps any exception thrown by the comparison delegate in an
                // InvalidOperationException (mirrors Activator.CreateInstance wrapping construction
                // failures in TargetInvocationException). Unwrap so callers see the CborException
                // thrown by CompareEntriesForDeterministicOrder for unsupported key types, not an
                // opaque "failed to compare two elements" message.
                throw cborException;
            }

            return entries;
        }

        private static int CompareEntriesForDeterministicOrder(KeyValuePair<TK, TV> x, KeyValuePair<TK, TV> y)
        {
            if (x.Key is string stringX && y.Key is string stringY)
            {
                return CborKeyComparer.CompareTextKeys(
                    Encoding.UTF8.GetBytes(stringX),
                    Encoding.UTF8.GetBytes(stringY));
            }

            if (x.Key is int intX && y.Key is int intY)
            {
                return CborKeyComparer.CompareIntKeys(intX, intY);
            }

            throw new CborException(
                $"Deterministic encoding supports only string and int dictionary keys; found {typeof(TK)}.");
        }

        public void ReadBeginMap(int size, ref ReaderContext context)
        {
            context.dict = InstantiateTempCollection();
        }

        public void ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            TK key = KeyConverter.Read(ref reader);
            TV value = ValueConverter.Read(ref reader);

            context.dict.Add(key, value);
        }

        public int GetMapSize(ref WriterContext context)
        {
            return context.lengthMode == LengthMode.IndefiniteLength ? -1 : context.count;
        }

        public bool WriteMapItem(ref CborWriter writer, ref WriterContext context)
        {
            if (context.enumerator.MoveNext())
            {
                KeyConverter.Write(ref writer, context.enumerator.Current.Key);
                ValueConverter.Write(ref writer, context.enumerator.Current.Value);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
