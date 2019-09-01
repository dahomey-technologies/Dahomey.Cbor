using System;

namespace Dahomey.Cbor.Util
{
    public static class TypeExtensions
    {
        public static object GetDefaultValue(this Type type)
        {
            if (type.IsClass)
            {
                return null;
            }

            return Activator.CreateInstance(type);
        }
    }
}
