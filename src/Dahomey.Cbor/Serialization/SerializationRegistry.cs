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

        public SerializationRegistry(CborOptions options)
        {
            ConverterRegistry = new CborConverterRegistry(options);
            ObjectMappingRegistry = new ObjectMappingRegistry(this);
            ObjectMappingConventionRegistry = new ObjectMappingConventionRegistry();
            DiscriminatorConventionRegistry = new DiscriminatorConventionRegistry(this);
        }
    }
}
