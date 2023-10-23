using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Conventions;
using System;

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
        UnixMilliseconds = 2,
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
        public static CborOptions Default { get; } = new CborOptions()
        {
            UnqualifiedTimeZoneDateTimeKind = DateTimeKind.Local
        };

        public SerializationRegistry Registry { get; private set; }
        public UnhandledNameMode UnhandledNameMode { get; set; }
        public ValueFormat EnumFormat { get; set; }
        public DateTimeFormat DateTimeFormat { get; set; }
        /// <summary>
        /// When an ISO date with an unqualified timezone is parsed, this option gives the DateTimeKind to use
        /// </summary>
        public DateTimeKind UnqualifiedTimeZoneDateTimeKind { get; set; }
        public CborDiscriminatorPolicy DiscriminatorPolicy { get; set; }
        public CborObjectFormat ObjectFormat { get; set; } = CborObjectFormat.StringKeyMap;
        public LengthMode ArrayLengthMode { get; set; } = LengthMode.DefiniteLength;
        public LengthMode MapLengthMode { get; set; } = LengthMode.DefiniteLength;
        /// <summary>
        /// Semantic Tag to check if the discriminator is present when ObjectFormat is Array
        /// </summary>
        /// Default value is 39 (see: https://github.com/lucas-clemente/cbor-specs/blob/master/id.md)
        public ulong DiscriminatorSemanticTag { get; set; } = 39;

        /// <summary>
        /// Default naming convention type
        /// </summary>
        /// <remarks>
        /// If type is marked 'CborNamingConventionAttribute' then will use naming convention from attribute
        /// This option is usefult when uses codegeneration. Just set up this options same on each side
        /// </remarks>
        public Type? DefaultNamingConventionType { get; set; }

    public CborOptions()
        {
            Registry = new SerializationRegistry(this);
        }
    }
}