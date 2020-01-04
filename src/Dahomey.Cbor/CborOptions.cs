using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;

namespace Dahomey.Cbor
{
    public enum UnhandledNameMode
    {
        Silent = 0,
        ThrowException = 1,
    }

    public enum ValueFormat
    {
        WriteToInt = 0,
        WriteToString = 1,
    }

    public enum DateTimeFormat
    {
        ISO8601 = 0,
        Unix = 1,
    }

    /// <summary>
    /// https://tools.ietf.org/html/rfc7049#section-2.2
    /// </summary>
    public enum LengthMode
    {
        Default = 0,
        DefiniteLength = 1,
        IndefiniteLength = 2
    }

    public class CborOptions
    {
        public static CborOptions Default { get; } = new CborOptions();

        public SerializationRegistry Registry { get; } = new SerializationRegistry();
        public UnhandledNameMode UnhandledNameMode { get; set; }
        public ValueFormat EnumFormat { get; set; }
        public DateTimeFormat DateTimeFormat { get; set; }
        public CborDiscriminatorPolicy DiscriminatorPolicy { get; set; }
        public LengthMode ArrayLengthMode { get; set; } = LengthMode.DefiniteLength;
        public LengthMode MapLengthMode { get; set; } = LengthMode.DefiniteLength;
    }
}