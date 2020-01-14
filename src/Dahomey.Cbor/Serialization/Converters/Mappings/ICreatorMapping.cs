using Dahomey.Cbor.Util;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters.Mappings
{
    public interface ICreatorMapping : IMappingInitialization
    {
        IReadOnlyCollection<RawString>? MemberNames { get; }
        object CreateInstance(Dictionary<RawString, object> values);

    }
}
