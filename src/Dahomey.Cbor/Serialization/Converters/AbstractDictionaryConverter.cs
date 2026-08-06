using System;
using System.Collections.Generic;

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
                    ? SortedEntries(value)
                    : value.GetEnumerator(),
                lengthMode = lengthMode != LengthMode.Default
                    ? lengthMode : _options.MapLengthMode
            };

            writer.WriteMap(this, ref context);
        }

        // The key set is only known at write time (unlike fixed object members, which ObjectConverter
        // sorts once per type), so this materialises and sorts on every write. GetMapSize/WriteMapItem
        // are untouched: they only ever pump context.enumerator, so handing them the sorted sequence's
        // enumerator instead of the dictionary's is enough.
        private IEnumerator<KeyValuePair<TK, TV>> SortedEntries(IDictionary<TK, TV> dictionary)
        {
            // KeyConverter is the same converter WriteMapItem uses, so the order is the order of the
            // bytes that actually get written -- including any converter the caller registered for the
            // key type themselves.
            KeyValuePair<TK, TV>[] sorted =
                DeterministicKeyOrder.Sort(dictionary, KeyConverter, _options.MaxDepth);

            return ((IEnumerable<KeyValuePair<TK, TV>>)sorted).GetEnumerator();
        }

        public void ReadBeginMap(int size, ref ReaderContext context)
        {
            context.dict = InstantiateTempCollection();
        }

        public void ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            TK key = KeyConverter.Read(ref reader);
            TV value = ValueConverter.Read(ref reader);

            // See CborValueConverter.ReadMapItem: these are malformed input, and were already refused
            // -- as an ArgumentException that escapes the CborException contract.
            try
            {
                context.dict.Add(key, value);
            }
            catch (ArgumentException exception)
            {
                throw reader.BuildException(DescribeAddFailure(context.dict, key, exception));
            }
        }

        /// <summary>
        /// Which of the three failures actually occurred. Only reached once an add has thrown, so the
        /// ContainsKey probe costs nothing on well-formed input.
        /// </summary>
        private static string DescribeAddFailure(IDictionary<TK, TV> dictionary, TK key, ArgumentException exception)
        {
            if (key is null)
            {
                return MapKeyErrors.NullKey();
            }

            return dictionary.ContainsKey(key)
                ? MapKeyErrors.Duplicate(key)
                : MapKeyErrors.Rejected(key, exception.Message);
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
