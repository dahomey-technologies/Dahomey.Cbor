using System.Runtime.CompilerServices;
using System.Text;

namespace Dahomey.Cbor.Serialization.Conventions;

internal static class NamingConventionExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetPropertyName(string name, byte separator, bool toUpper = false)
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
                    buffer[dstIndex++] = separator;
                    lastIsLower = false;
                }

                buffer[dstIndex++] = (byte)(toUpper ? c : char.ToLowerInvariant(c));
            }
            else
            {
                lastIsLower = true;
                buffer[dstIndex++] = (byte)(toUpper ? char.ToUpperInvariant(c) : c);
            }
        }

        return Encoding.UTF8.GetString(buffer, 0, dstIndex);
    }
}
