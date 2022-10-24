using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using System.Buffers;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class CborConverterAttributeTests
    {
        [Fact]
        public void CanConvertClass()
        {
            var input = new SomeClass { Value = 42 };
            var bytes = Serialize(input);
            var deserialized = Cbor.Deserialize<SomeClass>(bytes);
            Assert.Equal(42, deserialized.Value);
        }

        [Fact]
        public void CanConvertStruct()
        {
            var input = new SomeStruct(42);
            var bytes = Serialize(input);
            var deserialized = Cbor.Deserialize<SomeStruct>(bytes);
            Assert.Equal(42, deserialized.Value);
        }

        [Fact]
        public void CanConvertEnum()
        {
            var input = SomeEnum.RandomValue;
            var bytes = Serialize(input);
            var deserialized = Cbor.Deserialize<SomeEnum>(bytes);
            Assert.Equal(SomeEnum.RandomValue, deserialized);
        }

        [Fact]
        public void CanConvertInterface()
        {
            var input = (ISomeInterface)new SomeInterfaceImpl() { Value = 42 };
            var bytes = Serialize(input);
            var deserialized = Cbor.Deserialize<ISomeInterface>(bytes);
            Assert.Equal(42, deserialized.Value);
        }

        private static byte[] Serialize<T>(T input)
        {
            var writer = new ArrayBufferWriter<byte>();
            Cbor.Serialize(input, writer);
            return writer.WrittenSpan.ToArray();
        }


        [CborConverter(typeof(CborConverter))]
        class SomeClass
        {
            public int Value { get; set; }

            class CborConverter : CborConverterBase<SomeClass>
            {
                public override SomeClass Read(ref CborReader reader)
                {
                    return new SomeClass { Value = reader.ReadInt32() };
                }

                public override void Write(ref CborWriter writer, SomeClass value)
                {
                    writer.WriteInt32(value.Value);
                }
            }
        }

        [CborConverter(typeof(CborConverter))]
        readonly struct SomeStruct
        {
            public int Value { get; }

            public SomeStruct(int value)
            {
                Value = value;
            }

            class CborConverter : CborConverterBase<SomeStruct>
            {
                public override SomeStruct Read(ref CborReader reader)
                {
                    return new SomeStruct(reader.ReadInt32());
                }

                public override void Write(ref CborWriter writer, SomeStruct value)
                {
                    writer.WriteInt32(value.Value);
                }
            }
        }

        [CborConverter(typeof(SomeEnumCborConverter))]
        enum SomeEnum
        {
            RandomValue = 42
        }

        class SomeEnumCborConverter : CborConverterBase<SomeEnum>
        {
            public override SomeEnum Read(ref CborReader reader)
            {
                return (SomeEnum)reader.ReadInt32();
            }

            public override void Write(ref CborWriter writer, SomeEnum value)
            {
                writer.WriteInt32((int)value);
            }
        }

        interface ISomeInterface
        {
            int Value { get; set; }
        }

        class SomeInterfaceImpl : ISomeInterface
        {
            public int Value { get; set; }
        }

        class SomeInterfaceCborConverter : CborConverterBase<ISomeInterface>
        {
            public override ISomeInterface Read(ref CborReader reader)
            {
                return new SomeInterfaceImpl { Value = reader.ReadInt32() };
            }

            public override void Write(ref CborWriter writer, ISomeInterface value)
            {
                writer.WriteInt32(value.Value);
            }
        }
    }
}
