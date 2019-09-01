﻿using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IObjectMapping : IMappingInitialization
    {
        Type ObjectType { get; }
        INamingConvention NamingConvention { get; }
        IReadOnlyCollection<IMemberMapping> MemberMappings { get; }
        ICreatorMapping CreatorMapping { get;  }
    }
}
