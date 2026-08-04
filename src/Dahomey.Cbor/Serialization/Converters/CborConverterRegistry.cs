using Dahomey.Cbor.Serialization.Converters.Providers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class CborConverterRegistry
    {
        private readonly CborOptions _options;
        private readonly ConcurrentDictionary<Type, ICborConverter> _converters = new ConcurrentDictionary<Type, ICborConverter>();
        private readonly ConcurrentStack<ICborConverterProvider> _providers = new ConcurrentStack<ICborConverterProvider>();

        /// <summary>
        /// Types whose converter is being constructed on this thread, right now.
        /// </summary>
        /// <remarks>
        /// A converter resolves its members' converters while it is being constructed, and it is not
        /// in <c>_converters</c> until that constructor returns. Two types that reference each other
        /// therefore ask for each other indefinitely: A's construction looks up B, B's construction
        /// looks up A, and neither is registered yet. The result is an uncatchable
        /// <see cref="StackOverflowException"/>, not an exception a caller can handle.
        /// <para>
        /// Recording what is in flight lets a member mapping recognise the cycle and defer instead —
        /// see <c>MemberMapping{T}.InitializeConverter</c>. Thread-static because construction is
        /// re-entrant per thread; two threads building the same graph independently each complete on
        /// their own, and <see cref="ConcurrentDictionary{TKey, TValue}.GetOrAdd"/> settles which
        /// instance wins.
        /// </para>
        /// </remarks>
        [ThreadStatic]
        private static HashSet<(CborConverterRegistry, Type)>? _underConstruction;

        public CborConverterRegistry(CborOptions options)
        {
            _options = options;

            // order matters. It's in reverse order of how they'll get consumed
            RegisterConverterProvider(new ObjectConverterProvider());
            RegisterConverterProvider(new TupleConverterProvider());
            RegisterConverterProvider(new CollectionConverterProvider());
            RegisterConverterProvider(new PrimitiveConverterProvider());
            RegisterConverterProvider(new ByAttributeConverterProvider());
        }

        /// <summary>
        /// Gets the converter for the specified <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>
        /// The converter.
        /// </returns>
        public ICborConverter<T> Lookup<T>()
        {
            return (ICborConverter<T>)Lookup(typeof(T));
        }

        /// <summary>
        /// Gets the converter for the specified <paramref name="type" />.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        /// The converter.
        /// </returns>
        public ICborConverter Lookup(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("type");
            }

            if (type.IsGenericType && type.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    $"Generic type {type.FullName} has unassigned type parameters.",
                    "type");
            }

            return _converters.GetOrAdd(type, CreateConverter);
        }

        /// <summary>
        /// Registers the converter.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="converter">The converter.</param>
        public bool RegisterConverter(Type type, ICborConverter converter)
        {
            return _converters.TryAdd(type, converter);
        }

        /// <summary>
        /// Registers the converter provider. This behaves like a stack, so the 
        /// last provider registered is the first provider consulted.
        /// </summary>
        /// <param name="converterProvider">The converter provider.</param>
        public void RegisterConverterProvider(ICborConverterProvider converterProvider)
        {
            if (converterProvider == null)
            {
                throw new ArgumentNullException("converterProvider");
            }

            _providers.Push(converterProvider);
        }

        public void Reset()
        {
            _converters.Clear();
        }

        /// <summary>
        /// Whether a converter for <paramref name="type" /> is already being constructed further up
        /// this thread's call stack, so asking for it again would recurse rather than return.
        /// </summary>
        internal bool IsUnderConstruction(Type type)
        {
            return _underConstruction != null && _underConstruction.Contains((this, type));
        }

        private ICborConverter CreateConverter(Type type)
        {
            HashSet<(CborConverterRegistry, Type)> underConstruction =
                _underConstruction ??= new HashSet<(CborConverterRegistry, Type)>();

            underConstruction.Add((this, type));

            try
            {
                ICborConverter? converter = _providers
                    .Select(provider => provider.GetConverter(type, _options))
                    .FirstOrDefault(provider => provider != null);

                if (converter == null)
                {
                    throw new CborException($"No converter found for type {type.FullName}.");
                }

                return converter;
            }
            finally
            {
                underConstruction.Remove((this, type));
            }
        }
    }
}
