namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes <see cref="CborBigFloat"/> as an RFC 8949 §3.4.4 bigfloat, tag 5.
    /// </summary>
    public class BigFloatConverter : CborConverterBase<CborBigFloat>
    {
        public override CborBigFloat Read(ref CborReader reader)
        {
            return reader.ReadBigFloat();
        }

        public override void Write(ref CborWriter writer, CborBigFloat value)
        {
            writer.WriteBigFloat(value);
        }
    }
}
