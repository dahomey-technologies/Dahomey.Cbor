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

            /// <summary>
            /// Where the entry currently being read sits, and what it is keyed by once that much has
            /// been decoded. Only read when an entry has thrown, to name the position in
            /// <see cref="CborException.Path"/>.
            /// </summary>
            /// <remarks>
            /// The key is the better name of the two, so <see cref="index"/> - which counts entries
            /// from 0, and is -1 before the first one - is only used for a failure that happened
            /// before the key itself could be read.
            /// </remarks>
            public int index;

            /// <inheritdoc cref="index"/>
            public TK? key;

            /// <inheritdoc cref="index"/>
            public bool hasKey;

            /// <inheritdoc cref="CborValueConverter.MapReaderContext.lastWins"/>
            public bool lastWins;
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

            // Before the first entry, so that a failure on the map header itself is not attributed
            // to entry 0.
            ReaderContext context = new ReaderContext { index = -1 };

            try
            {
                reader.ReadMap(this, ref context);
            }
            catch (CborException exception)
            {
                string? key = context.hasKey ? DescribeKey(context.key) : null;

                if (key != null)
                {
                    exception.PrependPathMember(key);
                }
                else if (context.index >= 0)
                {
                    exception.PrependPathIndex(context.index);
                }
                else
                {
                    exception.MarkPathKnown();
                }

                throw;
            }

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
            context.lastWins = _options.DuplicateKeyMode == DuplicateKeyMode.LastWins;
        }

        public void ReadMapItem(ref CborReader reader, ref ReaderContext context)
        {
            context.index++;
            context.hasKey = false;

            TK key = KeyConverter.Read(ref reader);

            context.key = key;
            context.hasKey = true;

            TV value = ValueConverter.Read(ref reader);

            if (context.lastWins)
            {
                // An indexer assignment rather than Add, so a repeated key overwrites. Still guarded:
                // the dictionary is the caller's in the IDictionary case and may refuse an entry for
                // reasons of its own, and a null key is refused whatever the mode -- it is not a
                // duplicate, and there is no last occurrence of it to win.
                try
                {
                    context.dict[key] = value;
                }
                catch (ArgumentException exception)
                {
                    throw reader.BuildException(DescribeSetFailure(key, exception));
                }

                return;
            }

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
        /// How to name the key in a path, when naming it is all that is left to do.
        /// </summary>
        /// <remarks>
        /// <typeparamref name="TK"/> is the caller's type and its <c>ToString</c> is the caller's code,
        /// running here while an exception is already in flight. Letting it throw would replace the
        /// failure being reported with an unrelated one and lose the read error entirely.
        /// <para>
        /// Returning null hands the caller back to the entry's index, which says something true, rather
        /// than to an empty name, which would be indistinguishable from a key that really is the empty
        /// string.
        /// </para>
        /// <para>
        /// Named through <see cref="MapKeyErrors.KeyText"/> so that the path agrees with the message
        /// beside it. <typeparamref name="TK"/> may itself be a <c>CborValue</c>, which renders a text
        /// key quoted; describing it here directly reported one key two ways in one exception.
        /// </para>
        /// </remarks>
        private static string? DescribeKey(TK? key)
        {
            try
            {
                return key is null ? null : MapKeyErrors.KeyText(key);
            }
            catch (Exception)
            {
                return null;
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

        /// <summary>
        /// Which failure occurred on the <see cref="DuplicateKeyMode.LastWins"/> path, where an
        /// assignment cannot fail for being a repeat. A key already present is exactly what that mode
        /// asks the dictionary to accept, so the reason is never a duplicate and is not probed for.
        /// </summary>
        private static string DescribeSetFailure(TK key, ArgumentException exception)
        {
            return key is null
                ? MapKeyErrors.NullKey()
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
