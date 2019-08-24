using System;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions
{
    public class SnakeCaseNamingConvention : INamingConvention
    {
        public string GetPropertyName(string name)
        {
            byte[] buffer = new byte[name.Length * 2];
            int dstIndex = 0;
            int srcLength = name.Length;
            bool lastIsLower = false;

            for (int srcIndex = 0; srcIndex < srcLength; srcIndex++)
            {
                char c = name[srcIndex];

                if (char.IsUpper(c))
                {
                    if (lastIsLower || srcIndex > 0 && srcIndex < srcLength - 1 && char.IsLower(name[srcIndex + 1]))
                    {
                        buffer[dstIndex++] = (byte)'_';
                        lastIsLower = false;
                    }

                    buffer[dstIndex++] = (byte)char.ToLowerInvariant(c);
                }
                else
                {
                    lastIsLower = true;
                    buffer[dstIndex++] = (byte)c;
                }
            }

            return Encoding.UTF8.GetString(buffer, 0, dstIndex);
        }
    }
}
