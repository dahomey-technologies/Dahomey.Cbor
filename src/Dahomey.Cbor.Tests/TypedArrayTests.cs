using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Tests.Extensions;
using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
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

        [Fact]
        public void ReadFloatArrayBigEndian()
        {
            // D851 tag(81) 48 bytes(8) -> 1.5f, 2.5f big endian
            float[] value = Helper.Read<float[]>("D851483FC0000040200000");
            Assert.Equal(new[] { 1.5f, 2.5f }, value);
        }

        [Fact]
        public void ReadInt16ArrayBigEndian()
        {
            // D849 tag(73) 44 bytes(4) -> 1, -2 big endian
            short[] value = Helper.Read<short[]>("D849440001FFFE");
            Assert.Equal(new short[] { 1, -2 }, value);
        }

        [Fact]
        public void ReadDoubleArrayBigEndian()
        {
            // D852 tag(82) 48 bytes(8) -> 1.5d big endian
            double[] value = Helper.Read<double[]>("D852483FF8000000000000");
            Assert.Equal(new[] { 1.5d }, value);
        }

        [Fact]
        public void BigEndianAndLittleEndianDecodeIdentically()
        {
            Assert.Equal(
                Helper.Read<float[]>("D855480000C03F00002040"),
                Helper.Read<float[]>("D851483FC0000040200000"));
        }

        private static void AssertThrowsCborException(Action action)
        {
            // Converters are built through Activator.CreateInstance, so a CborException thrown while
            // building a mapping arrives wrapped in TargetInvocationException.
            Exception exception = Record.Exception(action);
            Assert.NotNull(exception);

            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is CborException)
                {
                    return;
                }
            }

            Assert.Fail($"Expected a CborException, got {exception}");
        }

        [Fact]
        public void PayloadLengthNotAMultipleOfElementSizeThrows()
        {
            // D855 tag(85) 43 bytes(3) -- 3 is not divisible by 4
            AssertThrowsCborException(() => Helper.Read<float[]>("D85543000000"));
        }

        [Fact]
        public void WrongElementTypeThrows()
        {
            // D856 tag(86) is binary64; reading it into float[] is corrupt data, not a conversion
            AssertThrowsCborException(() => Helper.Read<float[]>("D85648000000000000F83F"));
        }

        [Fact]
        public void ReservedTag76Throws()
        {
            // D84C tag(76) is reserved by RFC 8746
            AssertThrowsCborException(() => Helper.Read<short[]>("D84C4401000200"));
        }

        [Fact]
        public void Binary128TagThrows()
        {
            // D853 tag(83) 40 bytes(0) -- binary128, which has no .NET type
            AssertThrowsCborException(() => Helper.Read<double[]>("D85340"));
        }

        [Fact]
        public void ReadByteArrayWithUint8Tag()
        {
            // D840 tag(64) 42 bytes(2) -> 1, 2
            byte[] value = Helper.Read<byte[]>("D840420102");
            Assert.Equal(new byte[] { 1, 2 }, value);
        }

        [Fact]
        public void ReadByteArrayWithClampedUint8Tag()
        {
            // D844 tag(68) 42 bytes(2) -> 1, 2
            byte[] value = Helper.Read<byte[]>("D844420102");
            Assert.Equal(new byte[] { 1, 2 }, value);
        }

        [Fact]
        public void ByteArrayIsStillWrittenAsAPlainByteString()
        {
            // 42 bytes(2) 0102 -- never tag 64, because the plain form is shorter and idiomatic
            Helper.TestWrite(new byte[] { 1, 2 }, "420102", null, TypedArrayOptions());
        }

        [Fact]
        public void TypedArrayIsSubstantiallySmallerForRealisticSampleData()
        {
            // A thousand sensor samples, the shape this feature exists for. Values are deliberately
            // not representable as binary16, so the plain form cannot shrink each element to 3 bytes
            // and the comparison reflects real recorded data rather than a best case for either side.
            float[] samples = new float[1000];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = i * 0.37f;
            }

            int plainBytes = Helper.Write(samples).Length / 2;
            int typedBytes = Helper.Write(samples, TypedArrayOptions()).Length / 2;

            // D8 55 (tag 85, 2 bytes) + 59 0F A0 (byte-string header, 3 bytes, since the 4000-byte
            // payload needs a 2-byte length so the header itself is 3 bytes) + 4 bytes per element.
            Assert.Equal(2 + 3 + 4 * samples.Length, typedBytes);

            // The plain form pays a per-element header. Assert a floor rather than an exact number so
            // the test states the guarantee instead of pinning an incidental encoding detail.
            Assert.True(plainBytes > typedBytes,
                $"expected the typed array to be smaller; plain={plainBytes} typed={typedBytes}");
        }

        // A typed array is almost always reached as a member of an object rather than as the root
        // value, so the tag has to survive every probe the object read path performs before the
        // member's own converter runs. The three object formats take three different code paths.

        public class SamplesHolder
        {
            public float[] Samples { get; set; }
            public string Unit { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class SamplesHolderIntKeyMap
        {
            [CborProperty(1)]
            public float[] Samples { get; set; }
            [CborProperty(2)]
            public string Unit { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class SamplesHolderArray
        {
            [CborProperty(0)]
            public float[] Samples { get; set; }
            [CborProperty(1)]
            public string Unit { get; set; }
        }

        [Fact]
        public void StringKeyMapMemberRoundTrips()
        {
            // A2 map(2) 6753616D706C6573 "Samples" D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //           64556E6974 "Unit" 6156 "V"
            const string hexBuffer = "A26753616D706C6573D855480000C03F0000204064556E69746156";
            CborOptions options = TypedArrayOptions();

            SamplesHolder value = new SamplesHolder { Samples = new[] { 1.5f, 2.5f }, Unit = "V" };
            Helper.TestWrite(value, hexBuffer, null, options);

            SamplesHolder actual = Helper.Read<SamplesHolder>(hexBuffer, options);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual.Samples);
            Assert.Equal("V", actual.Unit);
        }

        [Fact]
        public void IntKeyMapMemberRoundTrips()
        {
            // A2 map(2) 01 1 D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //           02 2 6156 "V"
            const string hexBuffer = "A201D855480000C03F00002040026156";
            CborOptions options = TypedArrayOptions();

            SamplesHolderIntKeyMap value = new SamplesHolderIntKeyMap { Samples = new[] { 1.5f, 2.5f }, Unit = "V" };
            Helper.TestWrite(value, hexBuffer, null, options);

            SamplesHolderIntKeyMap actual = Helper.Read<SamplesHolderIntKeyMap>(hexBuffer, options);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual.Samples);
            Assert.Equal("V", actual.Unit);
        }

        [Fact]
        public void ArrayFormatMemberRoundTrips()
        {
            // 82 array(2) D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //             6156 "V"
            const string hexBuffer = "82D855480000C03F000020406156";
            CborOptions options = TypedArrayOptions();

            SamplesHolderArray value = new SamplesHolderArray { Samples = new[] { 1.5f, 2.5f }, Unit = "V" };
            Helper.TestWrite(value, hexBuffer, null, options);

            SamplesHolderArray actual = Helper.Read<SamplesHolderArray>(hexBuffer, options);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual.Samples);
            Assert.Equal("V", actual.Unit);
        }

        public struct SamplesStruct
        {
            public float[] Samples { get; set; }
            public int Id { get; set; }
        }

        [Fact]
        public void StructMemberRoundTrips()
        {
            // Structs go through StructMemberConverter, a separate read path from the class one.
            // A2 map(2) 6753616D706C6573 "Samples" D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //           624964 "Id" 0C 12
            const string hexBuffer = "A26753616D706C6573D855480000C03F000020406249640C";
            CborOptions options = TypedArrayOptions();

            SamplesStruct value = new SamplesStruct { Samples = new[] { 1.5f, 2.5f }, Id = 12 };
            Helper.TestWrite(value, hexBuffer, null, options);

            SamplesStruct actual = Helper.Read<SamplesStruct>(hexBuffer, options);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual.Samples);
            Assert.Equal(12, actual.Id);
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class Signal
        {
            [CborProperty(1)]
            public float[] Samples { get; set; }
        }

        [CborDiscriminator("Analog")]
        [CborObjectFormat(CborObjectFormat.Array)]
        public class AnalogSignal : Signal
        {
            [CborProperty(2)]
            public string Unit { get; set; }
        }

        private static CborOptions PolymorphicArrayOptions()
        {
            CborOptions options = new CborOptions
            {
                TypedArrayMode = TypedArrayMode.LittleEndian,
                DiscriminatorPolicy = CborDiscriminatorPolicy.Always
            };
            options.Registry.DiscriminatorConventionRegistry.RegisterType<AnalogSignal>();
            return options;
        }

        [Fact]
        public void ArrayFormatWithDiscriminatorTagRoundTripsTypedArrayMember()
        {
            // Two tags in one document: the discriminator tag on the object and an RFC 8746 tag on
            // the member nested inside it.
            // 83 array(3) D827 tag(39) 66416E616C6F67 "Analog"
            //             D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //             6156 "V"
            const string hexBuffer = "83D82766416E616C6F67D855480000C03F000020406156";
            CborOptions options = PolymorphicArrayOptions();

            Signal value = new AnalogSignal { Samples = new[] { 1.5f, 2.5f }, Unit = "V" };
            Helper.TestWrite(value, hexBuffer, null, options);

            Signal actual = Helper.Read<Signal>(hexBuffer, options);
            AnalogSignal analog = Assert.IsType<AnalogSignal>(actual);
            Assert.Equal(new[] { 1.5f, 2.5f }, analog.Samples);
            Assert.Equal("V", analog.Unit);
        }

        [Fact]
        public void ArrayFormatKeepsANonDiscriminatorTagForTheFirstItem()
        {
            // The object expects a discriminator tag as its first item but the document carries none,
            // so the RFC 8746 tag it finds instead belongs to the first member and must be left in
            // place for that member's converter.
            // 82 array(2) D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //             6156 "V"
            const string hexBuffer = "82D855480000C03F000020406156";

            AnalogSignal actual = Helper.Read<AnalogSignal>(hexBuffer, PolymorphicArrayOptions());
            Assert.Equal(new[] { 1.5f, 2.5f }, actual.Samples);
            Assert.Equal("V", actual.Unit);
        }

        [Fact]
        public void TypedArrayAsDictionaryValueRoundTrips()
        {
            // A1 map(1) 6161 "a" D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            const string hexBuffer = "A16161D855480000C03F00002040";
            CborOptions options = TypedArrayOptions();

            Dictionary<string, float[]> value = new Dictionary<string, float[]>
            {
                ["a"] = new[] { 1.5f, 2.5f }
            };
            Helper.TestWrite(value, hexBuffer, null, options);

            // Helper.Read compares the results of three readers for equality, which a collection of
            // arrays cannot satisfy - float[] has no value equality. Deserialize once instead.
            Dictionary<string, float[]> actual = Cbor.Deserialize<Dictionary<string, float[]>>(
                hexBuffer.HexToBytes(), options);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual["a"]);
        }

        [Fact]
        public void TypedArrayInAListRoundTrips()
        {
            // 82 array(2) D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //             D85548000040400000A040 tag(85) bytes(8) 3f, 5f
            const string hexBuffer = "82D855480000C03F00002040D85548000040400000A040";
            CborOptions options = TypedArrayOptions();

            List<float[]> value = new List<float[]>
            {
                new[] { 1.5f, 2.5f },
                new[] { 3f, 5f }
            };
            Helper.TestWrite(value, hexBuffer, null, options);

            List<float[]> actual = Cbor.Deserialize<List<float[]>>(hexBuffer.HexToBytes(), options);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual[0]);
            Assert.Equal(new[] { 3f, 5f }, actual[1]);
        }

        [Fact]
        public void TypedArrayInAnIndefiniteLengthArrayRoundTrips()
        {
            // An indefinite-length container probes for the break marker before every item, and that
            // probe must not consume the item's own tag.
            // 9F array(*) D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //             FF break
            const string hexBuffer = "9FD855480000C03F00002040FF";

            List<float[]> actual = Cbor.Deserialize<List<float[]>>(hexBuffer.HexToBytes(), TypedArrayOptions());
            Assert.Single(actual);
            Assert.Equal(new[] { 1.5f, 2.5f }, actual[0]);
        }

        [Fact]
        public void TypedArrayInAnIndefiniteLengthMapRoundTrips()
        {
            // BF map(*) 6161 "a" D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //           FF break
            const string hexBuffer = "BF6161D855480000C03F00002040FF";

            Dictionary<string, float[]> actual = Cbor.Deserialize<Dictionary<string, float[]>>(
                hexBuffer.HexToBytes(), TypedArrayOptions());
            Assert.Equal(new[] { 1.5f, 2.5f }, actual["a"]);
        }

        [Fact]
        public void TypedArrayInAnIndefiniteLengthTupleRoundTrips()
        {
            // Tuples run their own break probe before every item, on the same terms.
            // 9F array(*) D855480000C03F00002040 tag(85) bytes(8) 1.5f, 2.5f
            //             6156 "V" FF break
            const string hexBuffer = "9FD855480000C03F000020406156FF";

            (float[] samples, string unit) = Cbor.Deserialize<ValueTuple<float[], string>>(
                hexBuffer.HexToBytes(), TypedArrayOptions());
            Assert.Equal(new[] { 1.5f, 2.5f }, samples);
            Assert.Equal("V", unit);
        }
    }
}
