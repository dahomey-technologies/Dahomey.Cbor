using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<Type, IDiscriminatorConvention?> _conventionsByType = new ConcurrentDictionary<Type, IDiscriminatorConvention?>();

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
        }

        public IDiscriminatorConvention? GetConvention(Type type)
        {
            return _conventionsByType.GetOrAdd(type, t => InternalGetConvention(t));
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
        /// The entry has to be able to displace a <c>null</c>. A base type is asked about before any of
        /// its subtypes is registered whenever a member is declared as the base - reading one builds its
        /// converter, which resolves the convention - and that lookup caches "no convention" under the
        /// base type. A plain <c>TryAdd</c> then cannot get past it, so registering the subtype
        /// afterwards left the base type resolving to null for the life of the options: the call did
        /// nothing and said nothing, and the read went on failing exactly as it had before it.
        /// <para>
        /// An existing non-null answer is kept rather than overwritten. The registry holds one
        /// convention per declared type, so the first hierarchy to claim a supertype keeps it, which is
        /// the behaviour <c>TryAdd</c> already had for that case.
        /// </para>
        /// </remarks>
        private void Propagate(Type supertype, IDiscriminatorConvention convention)
        {
            _conventionsByType.AddOrUpdate(supertype, convention, (_, existing) => existing ?? convention);
        }
    }
}
