using System.Reflection;

namespace Dahomey.Cbor.Serialization.Conventions;

public class KebabCaseNamingConvention : INamingConvention
{
    public string GetPropertyName(MemberInfo member)
    {
        return NamingConventionExtensions.GetPropertyName(member.Name, (byte)'-');
    }
}
