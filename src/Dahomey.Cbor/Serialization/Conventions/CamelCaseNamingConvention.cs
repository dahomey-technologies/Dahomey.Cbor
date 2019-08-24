using System;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class CamelCaseNamingConvention : INamingConvention
    {
        public string GetPropertyName(string reflectionName)
        {
            if (string.IsNullOrEmpty(reflectionName) || char.IsLower(reflectionName[0]))
            {
                return reflectionName;
            }

            return char.ToLowerInvariant(reflectionName[0]) + reflectionName.Substring(1);
        }
    }
}
