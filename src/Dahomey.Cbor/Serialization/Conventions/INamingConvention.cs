namespace Dahomey.Cbor.Serialization.Conventions
{
    public interface INamingConvention
    {
        string GetPropertyName(string name);
    }
}
