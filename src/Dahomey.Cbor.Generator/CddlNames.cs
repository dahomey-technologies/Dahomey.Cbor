using Microsoft.CodeAnalysis;
using System.Linq;

namespace Dahomey.Cbor.Generator
{
    /// <summary>Identifier construction shared by the registration emitter and the CDDL emitter.</summary>
    internal static class CddlNames
    {
        /// <summary>
        /// Property name for a type's accessor: <c>Person</c>, and <c>ListOfPerson</c> for
        /// <c>List&lt;Person&gt;</c>, so closed generics get a legal, predictable identifier. Also the
        /// basis of CDDL rule names, which accept the same character set.
        /// </summary>
        public static string AccessorName(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                return "ArrayOf" + AccessorName(array.ElementType);
            }

            if (type is INamedTypeSymbol { IsGenericType: true } named)
            {
                string baseName = named.Name;
                string arguments = string.Concat(named.TypeArguments.Select(AccessorName));
                return $"{baseName}Of{arguments}";
            }

            return type.Name;
        }
    }
}
