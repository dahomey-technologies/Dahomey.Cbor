namespace Dahomey.Cbor.Serialization.Conventions;

public class KebabCaseNamingConvention : INamingConvention
{
    public string GetPropertyName(string name)
    {
        return NamingConventionExtensions.GetPropertyName(name, (byte)'-');
    }
}
