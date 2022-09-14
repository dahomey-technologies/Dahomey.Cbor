using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using Xunit;
using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Buffers;
using Nerdbank.Streams;

namespace Dahomey.Cbor.Tests
{
    public static class Helper
    {
        public static T Read<T>(string hexBuffer, CborOptions options = null)
        {
            options = options ?? CborOptions.Default;
            byte[] buffer = hexBuffer.HexToBytes();
            CborReader spanReader = new CborReader(buffer.AsSpan());
            ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
            
            T value = converter.Read(ref spanReader);

            CborReader sequenceReader = new CborReader(new ReadOnlySequence<byte>(buffer));
            T sequenceValue = converter.Read(ref sequenceReader);

            CborReader fragmentizedSequenceReader = new CborReader(Fragmentize(buffer));
            T fragmentizedSequenceValue = converter.Read(ref fragmentizedSequenceReader);

            if (typeof(T) == typeof(ReadOnlySequence<byte>))
            {
                var expected = ((ReadOnlySequence<byte>)(object)value).ToArray();
                Assert.Equal(expected, ((ReadOnlySequence<byte>)(object)sequenceValue).ToArray());
                Assert.Equal(expected, ((ReadOnlySequence<byte>)(object)fragmentizedSequenceValue).ToArray());
            }
            else
            {
                Assert.Equal(value, sequenceValue);
                Assert.Equal(value, fragmentizedSequenceValue);
            }

            return value;
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
                CborWriter writer = new CborWriter(bufferWriter);
                ICborConverter<T> converter = options.Registry.ConverterRegistry.Lookup<T>();
                converter.Write(ref writer, value);
                return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
            }
        }

        public static void TestWrite<T>(T value, string expectedHexBuffer, Type expectedExceptionType = null, CborOptions options = null)
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
                string actualHexBuffer = Write(value, options);
                Assert.Equal(expectedHexBuffer, actualHexBuffer);
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
            byte[] buffer = hexBuffer.HexToBytes();
            CborReader spanReader = new CborReader(buffer.AsSpan());
            MethodInfo method = typeof(CborReader).GetMethod(methodName, new Type[0] { });
            var functor = CreateMethodFunctor<CborReaderFunctor<T>>(method);
            T value = functor(spanReader);

            CborReader sequenceReader = new CborReader(new ReadOnlySequence<byte>(buffer));
            T sequenceValue = functor(sequenceReader);
            
            CborReader fragmentizedSequenceReader = new CborReader(Fragmentize(buffer));
            T fragmentizedSequenceValue = functor(fragmentizedSequenceReader);

            if (typeof(T) == typeof(ReadOnlySequence<byte>))
            {
                var expected = ((ReadOnlySequence<byte>)(object)value).ToArray();
                Assert.Equal(expected, ((ReadOnlySequence<byte>)(object)sequenceValue).ToArray());
                Assert.Equal(expected, ((ReadOnlySequence<byte>)(object)fragmentizedSequenceValue).ToArray());
            }
            else
            {
                Assert.Equal(value, sequenceValue);
                Assert.Equal(value, fragmentizedSequenceValue);
            }

            return value;
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
                else if (actualValue is ReadOnlySequence<byte> actualSequence)
                {
                    Assert.Equal(((ReadOnlySequence<byte>)(object)expectedValue).ToArray(), actualSequence.ToArray());
                }
                else
                {
                    Assert.Equal(expectedValue, actualValue);
                }
            }
        }

        delegate void CborWriterFunctor<T>(CborWriter writer, T value);

        public static string Write<T>(string methodName, T value)
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                CborWriter writer = new CborWriter(bufferWriter);
                MethodInfo method = typeof(CborWriter).GetMethod(methodName, new[] { typeof(T) });
                CreateMethodFunctor<CborWriterFunctor<T>>(method)(writer, value);
                return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
            }
        }

        public static void TestWrite<T>(string methodName, T value, string hexBuffer, Type expectedExceptionType = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Write(methodName, value);
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                }
            }
            else
            {
                Assert.Equal(hexBuffer, Write(methodName, value));
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

        public static ReadOnlySequence<byte> Fragmentize(ReadOnlySpan<byte> bytes)
        {
            var pool = new FakePool();
            var writer = new Sequence<byte>(pool);

            foreach (var @byte in bytes)
            {
                var span = writer.GetSpan(1);
                span[0] = @byte;
                writer.Advance(1);
            }

            return writer.AsReadOnlySequence;
        }

        class FakePool : MemoryPool<byte>
        {
            public override int MaxBufferSize => 1;

            public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
            {
                if (minBufferSize == -1)
                {
                    minBufferSize = 1;
                }
                return new Lease(new byte[minBufferSize]);
            }

            protected override void Dispose(bool disposing)
            {
            }

            class Lease : IMemoryOwner<byte>
            {
                public Memory<byte> Memory { get; }

                public Lease(Memory<byte> memory)
                {
                    Memory = memory;
                }

                public void Dispose()
                {
                }
            }
        }
    }
}
