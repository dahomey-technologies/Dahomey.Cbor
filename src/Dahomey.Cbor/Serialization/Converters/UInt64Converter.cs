namespace Dahomey.Cbor.Serialization.Converters
{
    public class UInt64Converter : CborConverterBase<ulong>
    {
        public override ulong Read(ref CborReader reader)
        {
            return reader.ReadUInt64();
        }

        public override void Write(ref CborWriter writer, ulong value)
        {
            writer.WriteUInt64(value);
        }
    }
}
