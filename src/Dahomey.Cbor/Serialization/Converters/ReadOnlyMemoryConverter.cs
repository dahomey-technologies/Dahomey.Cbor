using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class ReadOnlyMemoryConverter : CborConverterBase<ReadOnlyMemory<byte>>
    {
        public override ReadOnlyMemory<byte> Read(ref CborReader reader)
        {
            return new ReadOnlyMemory<byte>(reader.ReadByteString().ToArray());
        }

        public override void Write(ref CborWriter writer, ReadOnlyMemory<byte> value)
        {
            writer.WriteByteString(value.Span);
        }
    }
}
