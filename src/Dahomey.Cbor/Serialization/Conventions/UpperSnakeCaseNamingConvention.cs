namespace Dahomey.Cbor.Serialization.Conventions;

public class UpperSnakeCaseNamingConvention : INamingConvention
{
    public string GetPropertyName(string name)
    {
        return NamingConventionExtensions.GetPropertyName(name, (byte)'_', true);
    }
}
