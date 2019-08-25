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

    public class CborOptions
    {
        public static CborOptions Default { get; } = new CborOptions();

        public SerializationRegistry Registry { get; } = new SerializationRegistry();
        public UnhandledNameMode UnhandledNameMode { get; set; }
        public ValueFormat EnumFormat { get; set; }
        public DateTimeFormat DateTimeFormat { get; set; }
        public bool IsIndented { get; set; }
        public IDiscriminatorConvention DiscriminatorConvention { get; set; }

        public CborOptions()
        {
            DiscriminatorConvention = Registry.DefaultDiscriminatorConvention;
        }
    }
}