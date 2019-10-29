using Dahomey.Cbor.Serialization.Converters;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class DiscriminatorConventionRegistry
    {
        private readonly SerializationRegistry _serializationRegistry;
        private readonly ConcurrentStack<IDiscriminatorConvention> _conventions = new ConcurrentStack<IDiscriminatorConvention>();
        public DefaultDiscriminatorConvention DefaultDiscriminatorConvention { get; }

        public DiscriminatorConventionRegistry(SerializationRegistry serializationRegistry)
        {
            _serializationRegistry = serializationRegistry;
            DefaultDiscriminatorConvention = new DefaultDiscriminatorConvention(_serializationRegistry);

            // order matters. It's in reverse order of how they'll get consumed
            RegisterConvention(DefaultDiscriminatorConvention);
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

        public void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (Type type in assembly.GetTypes().Where(t => t.IsClass))
            {
                IDiscriminatorConvention discriminatorConvention = _conventions.FirstOrDefault(c => c.TryRegisterType(type));

                if (discriminatorConvention == null)
                {
                    continue;
                }
            }
        }
    }
}
