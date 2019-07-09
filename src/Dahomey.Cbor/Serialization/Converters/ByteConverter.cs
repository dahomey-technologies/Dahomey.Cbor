namespace Dahomey.Cbor.Serialization.Converters
{
    public class ByteConverter : CborConverterBase<byte>
    {
        public override byte Read(ref CborReader reader)
        {
            return reader.ReadByte();
        }

        public override void Write(ref CborWriter writer, byte value)
        {
            writer.WriteByte(value);
        }
    }
}
