using Dahomey.Cbor.Util;
using System;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class TypedArrayTests
    {
        private static CborOptions TypedArrayOptions()
        {
            return new CborOptions { TypedArrayMode = TypedArrayMode.LittleEndian };
        }

        [Fact]
        public void WriteFloatArrayAsTypedArray()
        {
            // D855 tag(85) 48 bytes(8) -> 1.5f, 2.5f little endian
            Helper.TestWrite(new[] { 1.5f, 2.5f }, "D855480000C03F00002040", null, TypedArrayOptions());
        }

        [Fact]
        public void WriteDoubleArrayAsTypedArray()
        {
            // D856 tag(86) 48 bytes(8) -> 1.5d little endian
            Helper.TestWrite(new[] { 1.5d }, "D85648000000000000F83F", null, TypedArrayOptions());
        }

        [Fact]
        public void WriteInt16ArrayAsTypedArray()
        {
            // D84D tag(77) 44 bytes(4) -> 1, -2 little endian
            Helper.TestWrite(new short[] { 1, -2 }, "D84D440100FEFF", null, TypedArrayOptions());
        }

        [Fact]
        public void WriteEmptyArrayAsTypedArray()
        {
            // D855 tag(85) 40 bytes(0)
            Helper.TestWrite(new float[0], "D85540", null, TypedArrayOptions());
        }

        [Fact]
        public void WriteNullArrayIsStillNull()
        {
            // F6 null
            Helper.TestWrite<float[]>(null, "F6", null, TypedArrayOptions());
        }

        [Fact]
        public void DefaultOptionsStillWritePlainArrays()
        {
            // 82 array(2) F93E00 1.5 F94100 2.5  -- unchanged from before this feature existed
            Helper.TestWrite(new[] { 1.5f, 2.5f }, "82F93E00F94100");
        }

        [Fact]
        public void TypedArrayRoundTrips()
        {
            CborOptions options = TypedArrayOptions();
            float[] expected = new[] { 1.5f, 2.5f, float.MaxValue, float.Epsilon };

            byte[] bytes;
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                Cbor.Serialize(expected, bufferWriter, options);
                bytes = bufferWriter.WrittenSpan.ToArray();
            }

            float[] actual = Cbor.Deserialize<float[]>(bytes, options);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReadFloatArrayLittleEndian()
        {
            // D855 tag(85) 48 bytes(8) -> 1.5f, 2.5f little endian
            float[] value = Helper.Read<float[]>("D855480000C03F00002040");
            Assert.Equal(new[] { 1.5f, 2.5f }, value);
        }

        [Fact]
        public void ReadDoubleArrayLittleEndian()
        {
            // D856 tag(86) 48 bytes(8) -> 1.5d little endian
            double[] value = Helper.Read<double[]>("D85648000000000000F83F");
            Assert.Equal(new[] { 1.5d }, value);
        }

        [Fact]
        public void ReadInt16ArrayLittleEndian()
        {
            // D84D tag(77) 44 bytes(4) -> 1, -2 little endian
            short[] value = Helper.Read<short[]>("D84D440100FEFF");
            Assert.Equal(new short[] { 1, -2 }, value);
        }

        [Fact]
        public void ReadSByteArray()
        {
            // D848 tag(72) 42 bytes(2) -> 1, -2
            sbyte[] value = Helper.Read<sbyte[]>("D8484201FE");
            Assert.Equal(new sbyte[] { 1, -2 }, value);
        }

        [Fact]
        public void ReadInt64ArrayLittleEndian()
        {
            // D84F tag(79) 48 bytes(8) -> -2 little endian
            long[] value = Helper.Read<long[]>("D84F48FEFFFFFFFFFFFFFF");
            Assert.Equal(new long[] { -2 }, value);
        }

        [Fact]
        public void ReadUInt16ArrayLittleEndian()
        {
            // D845 tag(69) 44 bytes(4) -> 1, 2 little endian
            ushort[] value = Helper.Read<ushort[]>("D8454401000200");
            Assert.Equal(new ushort[] { 1, 2 }, value);
        }

        [Fact]
        public void ReadInt32ArrayLittleEndian()
        {
            // D84E tag(78) 48 bytes(8) -> 1, -2 little endian
            int[] value = Helper.Read<int[]>("D84E4801000000FEFFFFFF");
            Assert.Equal(new[] { 1, -2 }, value);
        }

        [Fact]
        public void ReadUInt32ArrayLittleEndian()
        {
            // D846 tag(70) 44 bytes(4) -> 1 little endian
            uint[] value = Helper.Read<uint[]>("D8464401000000");
            Assert.Equal(new uint[] { 1 }, value);
        }

        [Fact]
        public void ReadUInt64ArrayLittleEndian()
        {
            // D847 tag(71) 48 bytes(8) -> 1 little endian
            ulong[] value = Helper.Read<ulong[]>("D847480100000000000000");
            Assert.Equal(new ulong[] { 1 }, value);
        }

        [Fact]
        public void ReadHalfArrayLittleEndian()
        {
            // D854 tag(84) 42 bytes(2) -> 1.5 little endian
            Half[] value = Helper.Read<Half[]>("D85442003E");
            Assert.Equal(new[] { (Half)1.5f }, value);
        }

        [Fact]
        public void ReadEmptyTypedArray()
        {
            // D855 tag(85) 40 bytes(0) -> empty
            float[] value = Helper.Read<float[]>("D85540");
            Assert.Empty(value);
        }

        [Fact]
        public void ReadPlainArrayStillWorks()
        {
            // 82 array(2) F93E00 1.5 F94100 2.5  -- preferred serialization shrinks both to half
            float[] value = Helper.Read<float[]>("82F93E00F94100");
            Assert.Equal(new[] { 1.5f, 2.5f }, value);
        }

        [Fact]
        public void UnrecognisedTagFallsThroughToPlainArray()
        {
            // D827 tag(39) 82 array(2) 01 02  -- tag 39 is not a typed array tag, so it is ignored
            int[] value = Helper.Read<int[]>("D827820102");
            Assert.Equal(new[] { 1, 2 }, value);
        }
    }
}
