using System;
using System.Reflection;

namespace Dahomey.Cbor.Util
{
    public static class PropertyInfoExtensions
    {
        public static Func<T, TP> GenerateGetter<T, TP>(this PropertyInfo propertyInfo)
        {
            if (propertyInfo.GetMethod == null)
            {
                throw new CborException("Cannot generate getter for property with no get method");
            }

            return (Func<T, TP>)propertyInfo.GetMethod.CreateDelegate(typeof(Func<T, TP>));
        }

        public static Action<T, TP> GenerateSetter<T, TP>(this PropertyInfo propertyInfo)
        {
            if (propertyInfo.SetMethod == null)
            {
                throw new CborException("Cannot generate Setter for property with no Set method");
            }

            return (Action<T, TP>)propertyInfo.SetMethod.CreateDelegate(typeof(Action<T, TP>));
        }
    }
}
