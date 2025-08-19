using System.Reflection;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public interface INamingConvention
    {
        string GetPropertyName(MemberInfo member);
    }
}
