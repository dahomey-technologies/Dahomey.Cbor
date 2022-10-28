using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization.Conventions;
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IObjectMapping
    {
        Type ObjectType { get; }
        INamingConvention? NamingConvention { get; }
        IReadOnlyCollection<IMemberMapping> MemberMappings { get; }
        ICreatorMapping? CreatorMapping { get;  }
        Delegate? OnSerializingMethod { get; }
        Delegate? OnSerializedMethod { get; }
        Delegate? OnDeserializingMethod { get; }
        Delegate? OnDeserializedMethod { get; }
        CborDiscriminatorPolicy DiscriminatorPolicy { get; }
        object? Discriminator { get; }
        LengthMode LengthMode { get; }

        void AutoMap();
        bool IsCreatorMember(ReadOnlySpan<byte> memberName);
    }
}
