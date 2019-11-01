﻿using Dahomey.Cbor.Serialization.Conventions;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Serialization.Converters.Mappings;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Dahomey.Cbor.Serialization
{
    public class SerializationRegistry
    {
        public CborConverterRegistry ConverterRegistry { get; }
        public ObjectMappingRegistry ObjectMappingRegistry { get; }
        public ObjectMappingConventionRegistry ObjectMappingConventionRegistry { get; }
        public DiscriminatorConventionRegistry DiscriminatorConventionRegistry { get; }

        public SerializationRegistry()
        {
            ConverterRegistry = new CborConverterRegistry(this);
            ObjectMappingRegistry = new ObjectMappingRegistry(this);
            ObjectMappingConventionRegistry = new ObjectMappingConventionRegistry();
            DiscriminatorConventionRegistry = new DiscriminatorConventionRegistry(this);
        }

        public void RegisterAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            foreach (Type type in assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsDefined(typeof(CompilerGeneratedAttribute))))
            {
                RegisterType(type);
            }
        }

        public void RegisterType(Type type)
        {
            // First call will force the registration.
            ConverterRegistry.Lookup(type);
        }
    }
}
