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
        private static readonly IDiscriminatorConvention defaultDiscriminatorConvention = new DefaultDiscriminatorConvention();
        public static CborOptions Default => new CborOptions();

        public UnhandledNameMode UnhandledNameMode { get; set; }
        public ValueFormat EnumFormat { get; set; }
        public DateTimeFormat DateTimeFormat { get; set; }
        public bool IsIndented { get; set; }
        public IDiscriminatorConvention DiscriminatorConvention { get; set; } = defaultDiscriminatorConvention;
    }
}
