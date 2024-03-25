namespace Dahomey.Cbor.Serialization.Conventions
{
    /// <summary>
    /// Convert all names to lower case when serializing
    /// </summary>
    /// <remarks> VariableName1 -> variablename1</remarks>
    public sealed class LowerCaseNamingConvention : INamingConvention
    {
        /// <summary>
        /// Get property name according convention
        /// </summary>
        /// <param name="name"> Property raw-name</param>
        /// <returns>Property name according convention</returns>
        public string GetPropertyName(string name)
        {
            return name.ToLower();
        }
    }
}
