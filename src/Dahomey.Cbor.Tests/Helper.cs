using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;

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
                    Assert.IsInstanceOfType(ex, expectedExceptionType);
                }
            }
            else
            {
                T actualValue = Read<T>(hexBuffer, options);
                if (actualValue is ICollection actualCollection)
                {
                    CollectionAssert.AreEqual((ICollection)expectedValue, actualCollection);
                }
                else
                {
                    Assert.AreEqual(expectedValue, actualValue);
                }
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
                    Assert.IsInstanceOfType(ex, expectedExceptionType);
                }
            }
            else
            {
                Assert.AreEqual(hexBuffer, Write(value, options));
            }
        }
    }
}
