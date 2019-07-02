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
        public static T Read<T>(string hexBuffer)
        {
            Span<byte> buffer = hexBuffer.HexToBytes();
            CborReader reader = new CborReader(buffer, null);
            ICborConverter<T> converter = CborConverter.Lookup<T>();
            return converter.Read(ref reader);
        }

        public static void TestRead<T>(string hexBuffer, T expectedValue, Type expectedExceptionType = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Read<T>(hexBuffer);
                }
                catch (Exception ex)
                {
                    Assert.IsInstanceOfType(ex, expectedExceptionType);
                }
            }
            else
            {
                T actualValue = Read<T>(hexBuffer);
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

        public static string Write<T>(T value, CborSerializationSettings settings = null)
        {
            ByteBufferWriter bufferWriter = new ByteBufferWriter();
            CborWriter writer = new CborWriter(bufferWriter, settings);
            ICborConverter<T> converter = CborConverter.Lookup<T>();
            converter.Write(ref writer, value);
            return BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", "");
        }

        public static void TestWrite<T>(T value, string hexBuffer, Type expectedExceptionType = null, CborSerializationSettings settings = null)
        {
            if (expectedExceptionType != null)
            {
                try
                {
                    Write(value, settings);
                }
                catch (Exception ex)
                {
                    Assert.IsInstanceOfType(ex, expectedExceptionType);
                }
            }
            else
            {
                Assert.AreEqual(hexBuffer, Write(value, settings));
            }
        }
    }
}
