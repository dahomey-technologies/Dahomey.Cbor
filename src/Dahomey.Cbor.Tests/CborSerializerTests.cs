using Dahomey.Cbor.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborSerializerTests
    {
        private const string SimpleObjectHexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";

        private readonly static SimpleObject SimpleObject = new SimpleObject
        {
            Boolean = true,
            Byte = 12,
            SByte = 13,
            Int16 = 14,
            UInt16 = 15,
            Int32 = 16,
            UInt32 = 17u,
            Int64 = 18,
            UInt64 = 19ul,
            Single = 20.21f,
            Double = 22.23,
            String = "string",
            DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
            Enum = EnumTest.Value1
        };

        private readonly static CborSerializationSettings Settings = new CborSerializationSettings
        {
            EnumFormat = ValueFormat.WriteToString
        };

        [TestMethod]
        public void DeserializeFromMemoryStream()
        {
            MemoryStream stream = new MemoryStream(SimpleObjectHexBuffer.HexToBytes());
            SimpleObject obj = CborSerializer.Deserialize<SimpleObject>(stream);
            TestSimpleObject(obj);
        }

        [TestMethod]
        public void SerializeToMemoryStream()
        {
            MemoryStream stream = new MemoryStream();
            CborSerializer.Serialize(SimpleObject, stream, Settings);
            TestBuffer(stream.ToArray());
        }

        [TestMethod]
        public void DeserializeFromFileStream()
        {
            string tempFileName = Path.GetTempFileName();
            File.WriteAllBytes(tempFileName, SimpleObjectHexBuffer.HexToBytes());

            try
            {
                using (FileStream stream = File.OpenRead(tempFileName))
                {
                    SimpleObject obj = CborSerializer.Deserialize<SimpleObject>(stream);
                    TestSimpleObject(obj);
                }
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [TestMethod]
        public void SerializeToFileStream()
        {
            string tempFileName = Path.GetTempFileName();

            try
            {
                using (FileStream stream = File.OpenWrite(tempFileName))
                {
                    CborSerializer.Serialize(SimpleObject, stream, Settings);
                }

                byte[] actualBuffer = File.ReadAllBytes(tempFileName);
                TestBuffer(actualBuffer);
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [TestMethod]
        public void DeserializeFromSpan()
        {
            Span<byte> buffer = SimpleObjectHexBuffer.HexToBytes();
            SimpleObject obj = CborSerializer.Deserialize<SimpleObject>(buffer);
            TestSimpleObject(obj);
        }

        [TestMethod]
        public void SerializerToBufferWriter()
        {
            ByteBufferWriter bufferWriter = new ByteBufferWriter();
            CborSerializer.Serialize(SimpleObject, bufferWriter, Settings);
            TestBuffer(bufferWriter.WrittenSpan.ToArray());
        }

        private void TestBuffer(byte[] actualBuffer)
        {
            string actualHexBuffer = BitConverter.ToString(actualBuffer).Replace("-", "");
            Assert.AreEqual(SimpleObjectHexBuffer, actualHexBuffer);
        }

        private void TestSimpleObject(SimpleObject obj)
        {
            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.Boolean);
            Assert.AreEqual(12, obj.Byte);
            Assert.AreEqual(13, obj.SByte);
            Assert.AreEqual(14, obj.Int16);
            Assert.AreEqual(15, obj.UInt16);
            Assert.AreEqual(16, obj.Int32);
            Assert.AreEqual(17u, obj.UInt32);
            Assert.AreEqual(18, obj.Int64);
            Assert.AreEqual(19ul, obj.UInt64);
            Assert.AreEqual(20.21f, obj.Single);
            Assert.AreEqual(22.23, obj.Double);
            Assert.AreEqual("string", obj.String);
            Assert.AreEqual(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.AreEqual(EnumTest.Value1, obj.Enum);
        }
    }
}
