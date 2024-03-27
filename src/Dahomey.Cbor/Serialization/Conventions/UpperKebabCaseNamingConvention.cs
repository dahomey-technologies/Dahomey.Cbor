namespace Dahomey.Cbor.Serialization.Conventions;

public class UpperKebabCaseNamingConvention : INamingConvention
{
    public string GetPropertyName(string name)
    {
        return NamingConventionExtensions.GetPropertyName(name, (byte)'-', true);
    }
}
