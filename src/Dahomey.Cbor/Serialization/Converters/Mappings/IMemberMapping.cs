using System;
using System.Reflection;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface IMemberMapping : IMappingInitialization
    {
        MemberInfo MemberInfo { get; }
        Type MemberType { get; }
        string MemberName { get; }
        ICborConverter Converter { get; }
        bool CanBeDeserialized { get; }
        bool CanBeSerialized { get; }
        object DefaultValue { get; }
        bool IgnoreIfDefault { get; }
        Func<object, bool> ShouldSerializeMethod { get; }
        LengthMode LengthMode { get; }
    }
}
