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

    /// <summary>
    /// Controls whether numeric arrays are written as RFC 8746 typed arrays.
    /// Reading typed arrays is always supported and is not affected by this setting.
    /// </summary>
    public enum TypedArrayMode
    {
        /// <summary>
        /// Write numeric arrays as ordinary CBOR arrays of individually encoded items.
        /// </summary>
        Never,

        /// <summary>
        /// Write numeric arrays as little-endian typed arrays: tags 72, 69, 77, 70, 78, 71, 79, 84, 85 and 86.
        /// The payload is a byte-for-byte image of the array on little-endian hardware.
        /// </summary>
        LittleEndian,
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

        private LengthMode _arrayLengthMode = LengthMode.DefiniteLength;
        private LengthMode _mapLengthMode = LengthMode.DefiniteLength;
        private bool _deterministic;

        public LengthMode ArrayLengthMode
        {
            get => _arrayLengthMode;
            set
            {
                if (_deterministic)
                {
                    RejectIndefinite(value, nameof(ArrayLengthMode));
                }

                _arrayLengthMode = value;
            }
        }

        public LengthMode MapLengthMode
        {
            get => _mapLengthMode;
            set
            {
                if (_deterministic)
                {
                    RejectIndefinite(value, nameof(MapLengthMode));
                }

                _mapLengthMode = value;
            }
        }

        /// <summary>
        /// Produce RFC 8949 section 4.2.1 deterministic output: shortest-form arguments, preferred
        /// float serialization, definite lengths, and map keys sorted bytewise on their encoded form.
        /// </summary>
        /// <remarks>
        /// Deterministic mode refuses any setting that admits more than one encoding of the same
        /// value, because such a setting would leave the bytes — and therefore any hash over them —
        /// undetermined.
        /// </remarks>
        public bool Deterministic
        {
            get => _deterministic;
            set
            {
                // Guard on `value`, not on `_deterministic` — the field has not been assigned yet, so
                // the shared helper below would read the OLD state and never fire while enabling.
                if (value)
                {
                    RejectIndefinite(_arrayLengthMode, nameof(ArrayLengthMode));
                    RejectIndefinite(_mapLengthMode, nameof(MapLengthMode));
                }

                _deterministic = value;
            }
        }

        /// <summary>
        /// Rejects a length mode that admits more than one encoding of the same value. Callers decide
        /// whether deterministic mode is in force; this method does not consult <c>_deterministic</c>,
        /// because the property setter calls it while that field is still stale.
        /// </summary>
        private static void RejectIndefinite(LengthMode lengthMode, string propertyName)
        {
            if (lengthMode == LengthMode.IndefiniteLength)
            {
                throw new CborException(
                    $"{propertyName} cannot be {nameof(LengthMode.IndefiniteLength)} when {nameof(Deterministic)} is enabled: "
                    + "indefinite lengths admit more than one encoding of the same value.");
            }
        }

        public TypedArrayMode TypedArrayMode { get; set; } = TypedArrayMode.Never;
        /// <summary>
        /// Semantic Tag to check if the discriminator is present when ObjectFormat is Array
        /// </summary>
        /// Default value is 39 (see: https://github.com/lucas-clemente/cbor-specs/blob/master/id.md)
        public ulong DiscriminatorSemanticTag { get; set; } = 39;

        /// <summary>
        /// The default naming convention to use when no naming convention is specified.
        /// </summary>
        public INamingConvention? DefaultNamingConvention { get; set; }

        public CborOptions()
        {
            Registry = new SerializationRegistry(this);
        }
    }
}