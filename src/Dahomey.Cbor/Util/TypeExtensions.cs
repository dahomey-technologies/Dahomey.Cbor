﻿using System;
using System.Runtime.CompilerServices;

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

        public static bool IsAnonymous(this Type type)
        {
            return type.Namespace == null
                && type.IsSealed
                && type.BaseType == typeof(object)
                && !type.IsPublic
                && type.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }
    }
}
