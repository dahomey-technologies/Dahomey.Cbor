using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class DiscriminatorConventionRegistry
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ConcurrentStack<IDiscriminatorConvention> _conventions = new ConcurrentStack<IDiscriminatorConvention>();
        private readonly ConcurrentDictionary<Type, IDiscriminatorConvention> _conventionsByType = new ConcurrentDictionary<Type, IDiscriminatorConvention>();

        public DiscriminatorConventionRegistry(SerializationRegistry serializationRegistry)
        {
            _serializationRegistry = serializationRegistry;

            // order matters. It's in reverse order of how they'll get consumed
            RegisterConvention(new DefaultDiscriminatorConvention(_serializationRegistry));
        }

        /// <summary>
        /// Registers the convention.This behaves like a stack, so the 
        /// last convention registered is the first convention consulted.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="convention">The convention.</param>
        public void RegisterConvention(IDiscriminatorConvention convention)
        {
            if (convention == null)
            {
                throw new ArgumentNullException(nameof(convention));
            }

            _conventions.Push(convention);
        }

        public IDiscriminatorConvention GetConvention(Type type)
        {
            return _conventionsByType.GetOrAdd(type, t => InternalGetConvention(t));
        }

        public void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (Type type in assembly.GetTypes()
                .Where(t => t.IsClass 
                    && !t.IsAbstract 
                    && !t.IsGenericTypeDefinition
                    && !t.IsDefined(typeof(CompilerGeneratedAttribute))))
            {
                IDiscriminatorConvention convention = _conventions.FirstOrDefault(c => c.IsDiscriminatedType(type));

                if (convention != null)
                {
                    convention.TryRegisterType(type);

                    // setup discriminator for all base types
                    for (Type currentType = type.BaseType; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
                    {
                        _conventionsByType.TryAdd(currentType, convention);
                    }
                }
            }
        }

        public void RegisterType(Type type)
        {
            // First call will force the registration.
            GetConvention(type);
        }

        private IDiscriminatorConvention InternalGetConvention(Type type)
        {
            IDiscriminatorConvention convention = _conventions.FirstOrDefault(c => c.TryRegisterType(type));

            if (convention != null)
            {
                // setup discriminator for all base types
                for (Type currentType = type.BaseType; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
                {
                    _conventionsByType.TryAdd(currentType, convention);
                }
            }

            return convention;
        }
    }
}
