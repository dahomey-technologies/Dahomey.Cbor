using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IObjectMapping
    {
        Type ObjectType { get; }
        INamingConvention NamingConvention { get; }
        IReadOnlyCollection<IMemberMapping> MemberMappings { get; }
        ICreatorMapping CreatorMapping { get;  }
    }
}
