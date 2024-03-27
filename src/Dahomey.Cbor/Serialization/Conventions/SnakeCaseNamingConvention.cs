namespace Dahomey.Cbor.Serialization.Conventions;

public class SnakeCaseNamingConvention : INamingConvention
{
    public string GetPropertyName(string name)
    {
        return NamingConventionExtensions.GetPropertyName(name, (byte)'_');
    }
}
