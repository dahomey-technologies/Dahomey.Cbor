namespace Dahomey.Cbor.Serialization.Conventions
{
    /// <summary>
    /// Convert all names to upper case when serializing
    /// </summary>
    /// <remarks> VariableName1 -> VARIABLENAME1</remarks>
    public sealed class UpperCaseNamingConvention : INamingConvention
    {
        /// <summary>
        /// Get property name according convention
        /// </summary>
        /// <param name="name"> Property raw-name</param>
        /// <returns>Property name according convention</returns>
        public string GetPropertyName(string name)
        {
            return name.ToUpper();
        }
    }
}
