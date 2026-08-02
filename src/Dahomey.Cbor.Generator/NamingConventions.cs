using System.Text;

namespace Dahomey.Cbor.Generator
{
    /// <summary>
    /// Compile-time reimplementation of Dahomey.Cbor's naming conventions.
    /// </summary>
    /// <remarks>
    /// The generated path bakes member names into the emitted code, so it cannot call
    /// <c>INamingConvention</c> at run time; the algorithms have to be duplicated here. They must stay
    /// byte-for-byte identical to <c>Serialization/Conventions/NamingConventionExtensions.cs</c> and the
    /// convention classes beside it — the generated-vs-reflection byte-identity tests are what enforce
    /// that.
    /// </remarks>
    internal static class NamingConventions
    {
        /// <summary>
        /// Applies the convention named by a <c>[CborSourceGenerationOptions(NamingConvention = ...)]</c>
        /// type name.
        /// </summary>
        /// <param name="conventionTypeName">
        /// Simple type name, e.g. "CamelCaseNamingConvention". Null or unrecognised leaves the name as
        /// declared; the caller validates and reports unrecognised names.
        /// </param>
        public static string Apply(string? conventionTypeName, string memberName)
        {
            switch (conventionTypeName)
            {
                case "CamelCaseNamingConvention":
                    return CamelCase(memberName);
                case "SnakeCaseNamingConvention":
                    return Separated(memberName, (byte)'_', toUpper: false);
                case "UpperSnakeCaseNamingConvention":
                    return Separated(memberName, (byte)'_', toUpper: true);
                case "KebabCaseNamingConvention":
                    return Separated(memberName, (byte)'-', toUpper: false);
                case "UpperKebabCaseNamingConvention":
                    return Separated(memberName, (byte)'-', toUpper: true);
                case "LowerCaseNamingConvention":
                    return memberName.ToLower();
                case "UpperCaseNamingConvention":
                    return memberName.ToUpper();
                default:
                    return memberName;
            }
        }

        /// <summary>Whether the convention is one this generator can reproduce.</summary>
        public static bool IsSupported(string conventionTypeName)
        {
            switch (conventionTypeName)
            {
                case "CamelCaseNamingConvention":
                case "SnakeCaseNamingConvention":
                case "UpperSnakeCaseNamingConvention":
                case "KebabCaseNamingConvention":
                case "UpperKebabCaseNamingConvention":
                case "LowerCaseNamingConvention":
                case "UpperCaseNamingConvention":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Mirrors <c>CamelCaseNamingConvention.GetPropertyName</c>.</summary>
        private static string CamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            {
                return name;
            }

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        /// <summary>Mirrors <c>NamingConventionExtensions.GetPropertyName</c>.</summary>
        private static string Separated(string name, byte separator, bool toUpper)
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
}
