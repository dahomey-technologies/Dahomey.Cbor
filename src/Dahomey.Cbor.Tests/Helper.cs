using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using Xunit;
using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Dahomey.Cbor.Tests
{
    public static class Helper
    {
        public static T Read<T>(string hexBuffer, CborOptions options = null)
        {
            options = options ?? CborOptions.Default;
            Span<byte> buffer = hexBuffer.HexToBytes();
            CborReader reader = new CborReader(buffer, options);
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            return converter.Read(ref reader);
        }

        public static void TestRead<T>(string hexBuffer, T expectedValue, Type expectedExceptionType = null, CborOptions options = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Read<T>(hexBuffer, options);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                }
            }
            else
            {
                T actualValue = Read<T>(hexBuffer, options);
                if (actualValue is ICollection actualCollection)
                {
                    Assert.Equal((ICollection)expectedValue, actualCollection);
                }
                else
                {
                    Assert.Equal(expectedValue, actualValue);
                }
            }
        }

        public static void TestRead<T>(string hexBuffer, Type expectedExceptionType = null, CborOptions options = null)
        {
            if (expectedExceptionType != null)
            {
                bool exceptionCatched = false;

                try
                {
                    Read<T>(hexBuffer, options);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                    exceptionCatched = true;
                }

                Assert.True(exceptionCatched, $"Expected exception {expectedExceptionType}");
            }
            else
            {
                Read<T>(hexBuffer, options);
            }
        }

        public static string Write<T>(T value, CborOptions options = null)
        {
            options = options ?? CborOptions.Default;

            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                CborWriter writer = new CborWriter(bufferWriter, options);
                ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
                converter.Write(ref writer, value);
                return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
            }
        }

        public static void TestWrite<T>(T value, string hexBuffer, Type expectedExceptionType = null, CborOptions options = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Write(value, options);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                }
            }
            else
            {
                Assert.Equal(hexBuffer, Write(value, options));
            }
        }

        public static void TestWrite<T>(T value, Type expectedExceptionType = null, CborOptions options = null)
        {
            if (expectedExceptionType != null)
            {
                bool exceptionCatched = false;

                try
                {
                    Write(value, options);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                    exceptionCatched = true;
                }

                Assert.True(exceptionCatched, $"Expected exception {expectedExceptionType}");
            }
            else
            {
                Write(value, options);
            }
        }

        delegate T CborReaderFunctor<T>(CborReader reader);

        public static T Read<T>(string methodName, string hexBuffer)
        {
            Span<byte> buffer = hexBuffer.HexToBytes();
            CborReader reader = new CborReader(buffer, null);
            MethodInfo method = typeof(CborReader).GetMethod(methodName, new Type[0] { });
            return CreateMethodFunctor<CborReaderFunctor<T>>(method)(reader);
        }

        public static void TestRead<T>(string methodName, string hexBuffer, T expectedValue, Type expectedExceptionType = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Read<T>(methodName, hexBuffer);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                }
            }
            else
            {
                T actualValue = Read<T>(methodName, hexBuffer);
                if (actualValue is ICollection actualCollection)
                {
                    Assert.Equal((ICollection)expectedValue, actualCollection);
                }
                else
                {
                    Assert.Equal(expectedValue, actualValue);
                }
            }
        }

        delegate void CborWriterFunctor<T>(CborWriter writer, T value);

        public static string Write<T>(string methodName, T value, CborOptions options = null)
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                CborWriter writer = new CborWriter(bufferWriter, options);
                MethodInfo method = typeof(CborWriter).GetMethod(methodName, new[] { typeof(T) });
                CreateMethodFunctor<CborWriterFunctor<T>>(method)(writer, value);
                return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
            }
        }

        public static void TestWrite<T>(string methodName, T value, string hexBuffer, Type expectedExceptionType = null, CborOptions options = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Write(methodName, value, options);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                }
            }
            else
            {
                Assert.Equal(hexBuffer, Write(methodName, value, options));
            }
        }

        private static TDelegate CreateMethodFunctor<TDelegate>(MethodInfo method) where TDelegate : Delegate
        {
            MethodInfo invokeMethod = typeof(TDelegate).GetMethod("Invoke");
            if (invokeMethod == null)
            {
                throw new InvalidOperationException($"{typeof(TDelegate)} signature could not be determined.");
            }

            ParameterInfo[] delegateParameters = invokeMethod.GetParameters();
            if (delegateParameters.Length < 1)
            {
                throw new InvalidOperationException("Delegate must have at least one parameter.");
            }

            Type paramType = delegateParameters[0].ParameterType;

            ParameterExpression objParam = Expression.Parameter(paramType, "obj");
            ParameterExpression[] argParams = delegateParameters
                .Skip(1)
                .Select((p, i) => Expression.Parameter(p.ParameterType, $"arg{i - 1}"))
                .ToArray();

            MethodCallExpression methodCallExpr = Expression.Call(objParam, method, argParams);
        
            Expression returnExpr = methodCallExpr;
            if (invokeMethod.ReturnType != methodCallExpr.Type)
            {
                returnExpr = Expression.ConvertChecked(methodCallExpr, invokeMethod.ReturnType);
            }

            Expression<TDelegate> lambda = Expression.Lambda<TDelegate>(
                returnExpr, 
                new[] { objParam }.Concat(argParams));

            return lambda.Compile();
        }
    }
}
