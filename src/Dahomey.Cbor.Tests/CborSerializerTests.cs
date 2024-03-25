using Dahomey.Cbor.Util;
using Xunit;
using System;
using System.IO;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Linq;
using System.Collections.Generic;

namespace Dahomey.Cbor.Tests
{

    public class CborSerializerTests
    {
        private const string SimpleObjectHexBuffer = "AF67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B67446563696D616CFC00000000000425D40000000000050000684461746554696D65C074323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
        private const string MultipleSimpleObjectHexBuffer = "AF67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B67446563696D616CFC00000000000425D40000000000050000684461746554696D65C074323031342D30322D32315431393A30303A30305A64456E756D6656616C756531AF67426F6F6C65616EF5655342797465183D6442797465183C65496E743136183E6655496E743136183F65496E74333218406655496E743332184165496E74363418426655496E743634184366537472696E6766737472696E676653696E676C65FA42875C2966446F75626C65FB40516CCCCCCCCCCD67446563696D616CFC00000000000AF1D90000000000050000684461746554696D65C074323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";

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
            Decimal = 2.71828m,
            String = "string",
            DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
            Enum = EnumTest.Value1
        };

        private readonly static SimpleObject SimpleObject2 = new SimpleObject
        {
            Boolean = true,
            Byte = 60,
            SByte = 61,
            Int16 = 62,
            UInt16 = 63,
            Int32 = 64,
            UInt32 = 65u,
            Int64 = 66,
            UInt64 = 67ul,
            Single = 67.68f,
            Double = 69.70,
            Decimal = 7.17273m,
            String = "string",
            DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
            Enum = EnumTest.Value1
        };

        private readonly static SimpleObject[] SimpleObjectArray = new SimpleObject[] { SimpleObject, SimpleObject2 };

        private readonly static CborOptions Options = new CborOptions
        {
            EnumFormat = ValueFormat.WriteToString
        };

        [Fact]
        public async Task DeserializeFromMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream(SimpleObjectHexBuffer.HexToBytes());
            SimpleObject obj = await Cbor.DeserializeAsync<SimpleObject>(stream);
            TestSimpleObject(obj);
        }

        [Fact]
        public async Task DeserializeObjectFromMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream(SimpleObjectHexBuffer.HexToBytes());
            SimpleObject obj = (SimpleObject)await Cbor.DeserializeAsync(typeof(SimpleObject), stream);
            TestSimpleObject(obj);
        }

#if !SKIP
        [Fact]
        public async Task DeserializeMultipleFromMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream(MultipleSimpleObjectHexBuffer.HexToBytes());
            SimpleObject[] obj = await Cbor.DeserializeMultipleAsync<SimpleObject>(stream);
            TestMultipleSimpleObject(obj);
        }

        [Fact]
        public async Task DeserializeMultipleObjectFromMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream(MultipleSimpleObjectHexBuffer.HexToBytes());
            object[] obj = await Cbor.DeserializeMultipleAsync(typeof(SimpleObject), stream);
            TestMultipleSimpleObject(obj);
        }
#endif

        [Fact]
        public async Task SerializeToMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream();
            await Cbor.SerializeAsync(SimpleObject, stream, Options);
            TestBuffer(stream.ToArray());
        }

        [Fact]
        public async Task SerializeOBjectToMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream();
            await Cbor.SerializeAsync(SimpleObject, typeof(SimpleObject), stream, Options);
            TestBuffer(stream.ToArray());
        }

        [Fact]
        public async Task SerializeMultipleToMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream();
            await Cbor.SerializeMultipleAsync(SimpleObjectArray, stream, Options);
            TestMultipleBuffer(stream.ToArray());
        }

        [Fact]
        public async Task SerializeMultipleObjectToMemoryStreamAsync()
        {
            MemoryStream stream = new MemoryStream();
            await Cbor.SerializeMultipleAsync(SimpleObjectArray, typeof(SimpleObject), stream, Options);
            TestMultipleBuffer(stream.ToArray());
        }

        [Fact]
        public async Task DeserializeFromFileStreamAsync()
        {
            string tempFileName = Path.GetTempFileName();
            File.WriteAllBytes(tempFileName, SimpleObjectHexBuffer.HexToBytes());

            try
            {
                using (FileStream stream = File.OpenRead(tempFileName))
                {
                    SimpleObject obj = await Cbor.DeserializeAsync<SimpleObject>(stream);
                    TestSimpleObject(obj);
                }
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [Fact]
        public async Task DeserializeObjectFromFileStreamAsync()
        {
            string tempFileName = Path.GetTempFileName();
            File.WriteAllBytes(tempFileName, SimpleObjectHexBuffer.HexToBytes());

            try
            {
                using (FileStream stream = File.OpenRead(tempFileName))
                {
                    SimpleObject obj = (SimpleObject)await Cbor.DeserializeAsync(typeof(SimpleObject), stream);
                    TestSimpleObject(obj);
                }
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [Fact]
        public async Task DeserializeMultipleFromFileStreamAsync()
        {
            string tempFileName = Path.GetTempFileName();
            File.WriteAllBytes(tempFileName, MultipleSimpleObjectHexBuffer.HexToBytes());

            try
            {
                using (FileStream stream = File.OpenRead(tempFileName))
                {
                    SimpleObject[] obj = await Cbor.DeserializeMultipleAsync<SimpleObject>(stream);
                    TestMultipleSimpleObject(obj);
                }
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [Fact]
        public async Task DeserializeMultipleObjectFromFileStreamAsync()
        {
            string tempFileName = Path.GetTempFileName();
            File.WriteAllBytes(tempFileName, MultipleSimpleObjectHexBuffer.HexToBytes());

            try
            {
                using (FileStream stream = File.OpenRead(tempFileName))
                {
                    object[] obj = await Cbor.DeserializeMultipleAsync(typeof(SimpleObject), stream);
                    TestMultipleSimpleObject(obj);
                }
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [Fact]
        public async Task SerializeObjectToFileStreamAsync()
        {
            string tempFileName = Path.GetTempFileName();

            try
            {
                using (FileStream stream = File.OpenWrite(tempFileName))
                {
                    await Cbor.SerializeAsync(SimpleObject, typeof(SimpleObject), stream, Options);
                }

                byte[] actualBuffer = File.ReadAllBytes(tempFileName);
                TestBuffer(actualBuffer);
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [Fact]
        public async Task SerializeMultipleObjectToFileStreamAsync()
        {
            string tempFileName = Path.GetTempFileName();

            try
            {
                using (FileStream stream = File.OpenWrite(tempFileName))
                {
                    await Cbor.SerializeMultipleAsync(SimpleObjectArray, typeof(SimpleObject), stream, Options);
                }

                byte[] actualBuffer = File.ReadAllBytes(tempFileName);
                TestMultipleBuffer(actualBuffer);
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }

        [Fact]
        public void DeserializeFromSpan()
        {
            Span<byte> buffer = SimpleObjectHexBuffer.HexToBytes();
            SimpleObject obj = Cbor.Deserialize<SimpleObject>(buffer);
            TestSimpleObject(obj);
        }

        [Fact]
        public void DeserializeObjectFromSpan()
        {
            Span<byte> buffer = SimpleObjectHexBuffer.HexToBytes();
            SimpleObject obj = (SimpleObject)Cbor.Deserialize(typeof(SimpleObject), buffer);
            TestSimpleObject(obj);
        }

        [Fact]
        public void DeserializeMultipleFromSpan()
        {
            Span<byte> buffer = MultipleSimpleObjectHexBuffer.HexToBytes();
            SimpleObject[] objs = Cbor.DeserializeMultiple<SimpleObject>(buffer);
            TestMultipleSimpleObject(objs);
        }

        [Fact]
        public void DeserializeMultipleObjectFromSpan()
        {
            Span<byte> buffer = MultipleSimpleObjectHexBuffer.HexToBytes();
            object[] objs = Cbor.DeserializeMultiple(typeof(SimpleObject), buffer);
            TestMultipleSimpleObject(objs);
        }

        [Fact]
        public void SerializeToBufferWriter()
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Cbor.Serialize(SimpleObject, bufferWriter, Options);
                TestBuffer(bufferWriter.WrittenSpan.ToArray());
            }
        }

        [Fact]
        public void SerializeObjectToBufferWriter()
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Cbor.Serialize(SimpleObject, typeof(SimpleObject), bufferWriter, Options);
                TestBuffer(bufferWriter.WrittenSpan.ToArray());
            }
        }

        [Fact]
        public void SerializeMultipleToBufferWriter()
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Cbor.SerializeMultiple(SimpleObjectArray, bufferWriter, Options);
                TestMultipleBuffer(bufferWriter.WrittenSpan.ToArray());
            }
        }

        [Fact]
        public void SerializeMultipleObjectToBufferWriter()
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                var list = SimpleObjectArray;
                Cbor.SerializeMultiple(SimpleObjectArray, typeof(SimpleObject), bufferWriter, Options);
                TestMultipleBuffer(bufferWriter.WrittenSpan.ToArray());
            }
        }


        [Fact]
        public void SerializeLinqEnumerabe()
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                var input = new[] { "hello", "world" }
                    .Where(x => x.StartsWith("h"));

                Cbor.Serialize(input, input.GetType(), bufferWriter, Options);
                Assert.Equal("816568656C6C6F", BitConverter.ToString(bufferWriter.WrittenSpan.ToArray()).Replace("-", ""));

                var deserialized = Cbor.Deserialize(input.GetType(), bufferWriter.WrittenSpan, Options);
                var list = Assert.IsType<List<string>>(deserialized);
                Assert.Single(list, "hello");
            }
        }


        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(512)]
        public async Task DeserializeFromPipeReaderAsync(int bufferSize)
        {
            ReadOnlyMemory<byte> cborBytes = SimpleObjectHexBuffer.HexToBytes();

            Pipe pipe = new Pipe();

            async Task WriteAsync(int sliceSize)
            {
                Memory<byte> buffer = pipe.Writer.GetMemory(cborBytes.Length);

                while (cborBytes.Length > 0)
                {
                    sliceSize = Math.Min(sliceSize, cborBytes.Length);
                    cborBytes.Slice(0, sliceSize).CopyTo(buffer.Slice(0, sliceSize));
                    cborBytes = cborBytes.Slice(sliceSize);
                    buffer = buffer.Slice(sliceSize);
                    pipe.Writer.Advance(sliceSize);
                    await pipe.Writer.FlushAsync();
                    await Task.Delay(1);
                }

                await pipe.Writer.CompleteAsync();
            }

            Task<SimpleObject> readTask = Cbor.DeserializeAsync<SimpleObject>(pipe.Reader).AsTask();

            await Task.WhenAll(WriteAsync(bufferSize), readTask);

            SimpleObject obj = await readTask;
            TestSimpleObject(obj);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(512)]
        public async Task DeserializeObjectFromPipeReaderAsync(int bufferSize)
        {
            ReadOnlyMemory<byte> cborBytes = SimpleObjectHexBuffer.HexToBytes();

            Pipe pipe = new Pipe();

            async Task WriteAsync(int sliceSize)
            {
                Memory<byte> buffer = pipe.Writer.GetMemory(cborBytes.Length);

                while (cborBytes.Length > 0)
                {
                    sliceSize = Math.Min(sliceSize, cborBytes.Length);
                    cborBytes.Slice(0, sliceSize).CopyTo(buffer.Slice(0, sliceSize));
                    cborBytes = cborBytes.Slice(sliceSize);
                    buffer = buffer.Slice(sliceSize);
                    pipe.Writer.Advance(sliceSize);
                    await pipe.Writer.FlushAsync();
                    await Task.Delay(1);
                }

                await pipe.Writer.CompleteAsync();
            }

            Task<object> readTask = Cbor.DeserializeAsync(typeof(SimpleObject), pipe.Reader).AsTask();

            await Task.WhenAll(WriteAsync(bufferSize), readTask);

            SimpleObject obj = (SimpleObject)await readTask;
            TestSimpleObject(obj);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(512)]
        public async Task DeserializeMultipleFromPipeReaderAsync(int bufferSize)
        {
            ReadOnlyMemory<byte> cborBytes = MultipleSimpleObjectHexBuffer.HexToBytes();

            Pipe pipe = new Pipe();

            async Task WriteAsync(int sliceSize)
            {
                Memory<byte> buffer = pipe.Writer.GetMemory(cborBytes.Length);

                while (cborBytes.Length > 0)
                {
                    sliceSize = Math.Min(sliceSize, cborBytes.Length);
                    cborBytes.Slice(0, sliceSize).CopyTo(buffer.Slice(0, sliceSize));
                    cborBytes = cborBytes.Slice(sliceSize);
                    buffer = buffer.Slice(sliceSize);
                    pipe.Writer.Advance(sliceSize);
                    await pipe.Writer.FlushAsync();
                    await Task.Delay(1);
                }

                await pipe.Writer.CompleteAsync();
            }

            Task<SimpleObject[]> readTask = Cbor.DeserializeMultipleAsync<SimpleObject>(pipe.Reader).AsTask();

            await Task.WhenAll(WriteAsync(bufferSize), readTask);

            SimpleObject[] obj = await readTask;
            TestMultipleSimpleObject(obj);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(32)]
        [InlineData(512)]
        public async Task DeserializeMultipleObjectFromPipeReaderAsync(int bufferSize)
        {
            ReadOnlyMemory<byte> cborBytes = MultipleSimpleObjectHexBuffer.HexToBytes();

            Pipe pipe = new Pipe();

            async Task WriteAsync(int sliceSize)
            {
                Memory<byte> buffer = pipe.Writer.GetMemory(cborBytes.Length);

                while (cborBytes.Length > 0)
                {
                    sliceSize = Math.Min(sliceSize, cborBytes.Length);
                    cborBytes.Slice(0, sliceSize).CopyTo(buffer.Slice(0, sliceSize));
                    cborBytes = cborBytes.Slice(sliceSize);
                    buffer = buffer.Slice(sliceSize);
                    pipe.Writer.Advance(sliceSize);
                    await pipe.Writer.FlushAsync();
                    await Task.Delay(1);
                }

                await pipe.Writer.CompleteAsync();
            }

            Task<object[]> readTask = Cbor.DeserializeMultipleAsync(typeof(SimpleObject), pipe.Reader).AsTask();

            await Task.WhenAll(WriteAsync(bufferSize), readTask);

            object[] obj = await readTask;
            TestMultipleSimpleObject(obj);
        }

        private void TestBuffer(byte[] actualBuffer)
        {
            string actualHexBuffer = BitConverter.ToString(actualBuffer).Replace("-", "");
            Assert.Equal(SimpleObjectHexBuffer, actualHexBuffer);
        }

        private void TestMultipleBuffer(byte[] actualBuffer)
        {
            string actualHexBuffer = BitConverter.ToString(actualBuffer).Replace("-", "");
            Assert.Equal(MultipleSimpleObjectHexBuffer, actualHexBuffer);
        }

        private void TestSimpleObject2(SimpleObject obj)
        {
            Assert.NotNull(obj);
            Assert.True(obj.Boolean);
            Assert.Equal(60, obj.Byte);
            Assert.Equal(61, obj.SByte);
            Assert.Equal(62, obj.Int16);
            Assert.Equal(63, obj.UInt16);
            Assert.Equal(64, obj.Int32);
            Assert.Equal(65u, obj.UInt32);
            Assert.Equal(66, obj.Int64);
            Assert.Equal(67ul, obj.UInt64);
            Assert.Equal(67.68f, obj.Single);
            Assert.Equal(69.70, obj.Double);
            Assert.Equal(7.17273m, obj.Decimal);
            Assert.Equal("string", obj.String);
            Assert.Equal(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.Equal(EnumTest.Value1, obj.Enum);
        }

        private void TestSimpleObject(SimpleObject obj)
        {
            Assert.NotNull(obj);
            Assert.True(obj.Boolean);
            Assert.Equal(12, obj.Byte);
            Assert.Equal(13, obj.SByte);
            Assert.Equal(14, obj.Int16);
            Assert.Equal(15, obj.UInt16);
            Assert.Equal(16, obj.Int32);
            Assert.Equal(17u, obj.UInt32);
            Assert.Equal(18, obj.Int64);
            Assert.Equal(19ul, obj.UInt64);
            Assert.Equal(20.21f, obj.Single);
            Assert.Equal(22.23, obj.Double);
            Assert.Equal(2.71828m, obj.Decimal);
            Assert.Equal("string", obj.String);
            Assert.Equal(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.Equal(EnumTest.Value1, obj.Enum);
        }

        private void TestMultipleSimpleObject(object[] objs)
        {
            TestSimpleObject((SimpleObject)objs[0]);
            TestSimpleObject2((SimpleObject)objs[1]);
        }
    }
}
