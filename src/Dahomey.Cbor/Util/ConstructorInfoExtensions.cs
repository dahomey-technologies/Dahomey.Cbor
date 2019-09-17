﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Dahomey.Cbor.Util
{
    public static class ConstructorInfoExtensions
    {
        public static Func<T> CreateDelegate<T>(this ConstructorInfo constructorInfo)
        {
            ParameterExpression[] parameters = constructorInfo.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            NewExpression body = Expression.New(constructorInfo, parameters);

            Expression<Func<T>> lambda = Expression.Lambda<Func<T>>(body, parameters);
            return lambda.Compile();

        }

        public static Delegate CreateDelegate(this ConstructorInfo constructorInfo)
        {
            ParameterExpression[] parameters = constructorInfo.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            NewExpression body = Expression.New(constructorInfo, parameters);

            LambdaExpression lambda = Expression.Lambda(body, parameters);
            return lambda.Compile();
        }
    }
}
