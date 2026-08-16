using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class DiscriminatorConventionRegistry
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ConcurrentStack<IDiscriminatorConvention> _conventions = new ConcurrentStack<IDiscriminatorConvention>();
        /// <summary>
        /// The convention resolved for a type. Only resolutions that found one are kept.
        /// </summary>
        /// <remarks>
        /// Caching "no convention" is what made <c>RegisterType</c> useless after the fact: a base type
        /// is asked about before any of its subtypes is registered whenever a member is declared as the
        /// base - reading one builds its converter, which resolves the convention - and the null cached
        /// there outlived every later registration, so the remedy the error message names failed on the
        /// very options the caller had in hand. Not keeping nulls costs a re-resolution per lookup of a
        /// type that has no discriminator, which is a couple of cached dictionary reads, and it is the
        /// callers that ask often - <see cref="Converters.ObjectConverter{T}"/> above all - that hold
        /// their own answer and only come back here when <see cref="Version"/> has moved.
        /// </remarks>
        private readonly ConcurrentDictionary<Type, IDiscriminatorConvention> _conventionsByType = new ConcurrentDictionary<Type, IDiscriminatorConvention>();

        /// <summary>
        /// Incremented whenever a registration makes a type resolve to a convention it did not resolve
        /// to before. Lets a holder of a resolved convention tell "still the same answer" from "worth
        /// asking again" without asking.
        /// </summary>
        /// <remarks>
        /// Only ever needed by a holder whose answer was <c>null</c>: resolution is monotone - a type
        /// that resolves to a convention keeps it, since nothing unregisters a type - so a non-null
        /// answer can be held forever, and a null one is the only one that can go stale.
        /// </remarks>
        private int _version;

        internal int Version => Volatile.Read(ref _version);

        public DiscriminatorConventionRegistry(SerializationRegistry serializationRegistry)
        {
            _serializationRegistry = serializationRegistry;

            // order matters. It's in reverse order of how they'll get consumed
            RegisterConvention(new DefaultDiscriminatorConvention<string>(_serializationRegistry));
            RegisterConvention(new DefaultDiscriminatorConvention<int>(_serializationRegistry));
        }

        public bool AnyConvention()
        {
            return _conventions.Count != 0;
        }

        /// <summary>
        /// The registered conventions, most recently registered first.
        /// </summary>
        /// <remarks>
        /// For asking a document what it carries when no convention resolved for the declared type,
        /// which is exactly the case where a subtype was never registered: the type says nothing, so
        /// the only way to tell a missing registration from a genuinely undiscriminated document is to
        /// look for what each convention would have written. Not public, because it exposes registration
        /// order, and nothing outside the read path has a use for it.
        /// </remarks>
        internal IEnumerable<IDiscriminatorConvention> Conventions => _conventions;

        /// <summary>
        /// Registers the convention.This behaves like a stack, so the 
        /// last convention registered is the first convention consulted.
        /// </summary>
        /// <param name="convention">The convention.</param>
        public void RegisterConvention(IDiscriminatorConvention convention)
        {
            if (convention == null)
            {
                throw new ArgumentNullException(nameof(convention));
            }

            _conventions.Push(convention);

            // A convention that was not there before can only add resolutions, never remove one, so
            // anything holding a null answer should ask again.
            Interlocked.Increment(ref _version);
        }

        public void ClearConventions()
        {
            _conventions.Clear();

            // The per-type cache has to go with them. It holds resolutions made by the conventions
            // being cleared, and GetConvention answers from it without asking anything -- so leaving it
            // would keep a cleared convention governing every type already resolved, and make a
            // convention registered afterwards inert for exactly those types. The window that closed in
            // was narrow and invisible: a previous RegisterType, an earlier read, or constructing a
            // CborSerializerContext, whose generated Configure builds every declared converter before
            // the caller can reach its Options.
            _conventionsByType.Clear();

            // Anything holding an answer of its own asks again, the same way RegisterConvention says so.
            Interlocked.Increment(ref _version);
        }

        public IDiscriminatorConvention? GetConvention(Type type)
        {
            if (_conventionsByType.TryGetValue(type, out IDiscriminatorConvention? convention))
            {
                return convention;
            }

            convention = InternalGetConvention(type);

            if (convention != null)
            {
                _conventionsByType.TryAdd(type, convention);
            }

            return convention;
        }

        public void RegisterType(Type type)
        {
            // First call will force the registration.
            GetConvention(type);
        }

        public void RegisterType<T>() where T : class => RegisterType(typeof(T));

        private IDiscriminatorConvention? InternalGetConvention(Type type)
        {
            IDiscriminatorConvention? convention = _conventions.FirstOrDefault(c => c.TryRegisterType(type));

            if (convention != null)
            {
                // setup discriminator for all base types
                for (Type? currentType = type.BaseType; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
                {
                    Propagate(currentType, convention);
                }

                // setup discriminator for all interfaces
                foreach (var @interface in type.GetInterfaces())
                {
                    Propagate(@interface, convention);
                }

                Interlocked.Increment(ref _version);
            }

            return convention;
        }

        /// <summary>
        /// Records <paramref name="convention"/> as the answer for a base type or interface of a type
        /// just registered, unless that supertype already has one.
        /// </summary>
        /// <remarks>
        /// The registry holds one convention per declared type, so the first hierarchy to claim a
        /// supertype keeps it. Nothing has to be displaced here: the only entries in the way used to be
        /// the cached nulls, which are no longer kept.
        /// </remarks>
        private void Propagate(Type supertype, IDiscriminatorConvention convention)
        {
            _conventionsByType.TryAdd(supertype, convention);
        }
    }
}
