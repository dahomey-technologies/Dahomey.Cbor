using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IMemberMapping
    {
        MemberInfo MemberInfo { get; }
        Type MemberType { get; }
        string MemberName { get; }
        ICborConverter MemberConverter { get; }
        bool CanBeDeserialized { get; }
        bool CanBeSerialized { get; }
        object DefaultValue { get; }
    }
}
