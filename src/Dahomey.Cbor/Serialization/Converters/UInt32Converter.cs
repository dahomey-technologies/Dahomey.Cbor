namespace Dahomey.Cbor.Serialization.Converters
{
    public class UInt32Converter : CborConverterBase<uint>
    {
        public override uint Read(ref CborReader reader)
        {
            return reader.ReadUInt32();
        }

        public override void Write(ref CborWriter writer, uint value)
        {
            writer.WriteUInt32(value);
        }
    }
}
