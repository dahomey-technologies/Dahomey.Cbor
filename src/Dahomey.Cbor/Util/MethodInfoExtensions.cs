using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Dahomey.Cbor.Util
{
    public static class MethodInfoExtensions
    {
        public static Delegate CreateDelegate(this MethodInfo methodInfo)
        {
            ParameterExpression[] parameters = methodInfo.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();

            MethodCallExpression body = Expression.Call(methodInfo, parameters);

            LambdaExpression lambda = Expression.Lambda(body, parameters);
            return lambda.Compile();
        }
    }
}
