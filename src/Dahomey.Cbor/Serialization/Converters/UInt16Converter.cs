namespace Dahomey.Cbor.Serialization.Converters
{
    public class UInt16Converter : CborConverterBase<ushort>
    {
        public override ushort Read(ref CborReader reader)
        {
            return reader.ReadUInt16();
        }

        public override void Write(ref CborWriter writer, ushort value)
        {
            writer.WriteUInt16(value);
        }
    }
}
