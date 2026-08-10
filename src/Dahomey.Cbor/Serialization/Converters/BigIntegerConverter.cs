using System.Numerics;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes <see cref="BigInteger"/> as an RFC 8949 §3.4.3 bignum, or as a basic integer
    /// where the value fits in one.
    /// </summary>
    public class BigIntegerConverter : CborConverterBase<BigInteger>
    {
        public override BigInteger Read(ref CborReader reader)
        {
            return reader.ReadBigInteger();
        }

        public override void Write(ref CborWriter writer, BigInteger value)
        {
            writer.WriteBigInteger(value);
        }
    }
}
