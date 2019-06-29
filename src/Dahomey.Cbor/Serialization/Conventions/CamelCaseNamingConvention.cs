using System;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class CamelCaseNamingConvention : INamingConvention
    {
        public ReadOnlyMemory<byte> GetPropertyName(string reflectionName)
        {
            if (string.IsNullOrEmpty(reflectionName) || char.IsLower(reflectionName[0]))
            {
                return Encoding.UTF8.GetBytes(reflectionName);
            }

            return Encoding.UTF8.GetBytes(char.ToLowerInvariant(reflectionName[0]) + reflectionName.Substring(1));
        }
    }
}
