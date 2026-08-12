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
    /// What reading a map does when the same key appears twice.
    /// </summary>
    /// <remarks>
    /// RFC 8949 §5.6 declines to settle this: it requires a protocol to define what happens on
    /// repeated keys and leaves rejecting, first-wins and last-wins all open to the decoder. So this
    /// is a library policy rather than a conformance question, and it is one setting for every decode
    /// target - the object model, dictionaries, and mapped classes with or without a creator mapping -
    /// because a mode that applied to only some of them would recreate the target-dependence it exists
    /// to remove.
    /// <para>
    /// First-wins is deliberately absent. It is a different operation from the other two - skipping a
    /// value already read rather than declining to keep it - and no protocol has yet asked for it here.
    /// </para>
    /// </remarks>
    public enum DuplicateKeyMode
    {
        /// <summary>
        /// A repeated key is a <see cref="CborException"/>, naming the key and the position it was
        /// read at. The default: silently keeping one of two values for the same key is the failure
        /// mode nobody notices, which is the wrong default for anything decoding untrusted frames.
        /// </summary>
        Reject = 0,

        /// <summary>
        /// The last occurrence of a key wins and earlier ones are discarded, for every target. For
        /// protocols that define last-wins - which §5.6 explicitly contemplates - and for upgrading
        /// from a version where mapped classes with settable members behaved this way unconditionally.
        /// </summary>
        LastWins = 1,
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
    /// Controls whether numeric arrays are read from, and written as, RFC 8746 typed arrays (tags 64-87).
    /// </summary>
    /// <remarks>
    /// Reading and writing are separate flags so that a peer's typed arrays can be accepted without
    /// emitting any. <see cref="Never"/> is the default and is a true no-op: with it, the library reads
    /// and writes exactly what it did before typed arrays existed.
    /// </remarks>
    [Flags]
    public enum TypedArrayMode
    {
        /// <summary>
        /// Typed arrays are neither read nor written. A tag in the range 64-87 is skipped like any other
        /// unrecognised semantic tag, so its content must be an ordinary CBOR array to be readable.
        /// </summary>
        Never = 0,

        /// <summary>
        /// Read RFC 8746 typed arrays, in either byte order, into the matching numeric array or collection.
        /// A tag whose element type does not match the target throws a <see cref="CborException"/>.
        /// </summary>
        Read = 1,

        /// <summary>
        /// Write numeric arrays as little-endian typed arrays: tags 72, 69, 77, 70, 78, 71, 79, 84, 85
        /// and 86. The payload is a byte-for-byte image of the array on little-endian hardware.
        /// </summary>
        WriteLittleEndian = 2,

        /// <summary>
        /// Read typed arrays and write little-endian ones.
        /// </summary>
        ReadWriteLittleEndian = Read | WriteLittleEndian,
    }

    /// <summary>
    /// Which of the two encodings a <see cref="decimal"/> is written as.
    /// </summary>
    /// <remarks>
    /// Reading is not affected and needs no setting: a <see cref="decimal"/> is read from either form
    /// whatever this says, so widening a service's input costs nothing and turning this on does not
    /// stop it reading the documents it wrote before.
    /// <para>
    /// Only writing has to choose, because the choice moves bytes: every <see cref="decimal"/> this
    /// library has ever written uses <see cref="DecimalFloat"/>, so that stays the default and no
    /// existing document changes shape underneath a caller who has not asked for it.
    /// </para>
    /// </remarks>
    public enum DecimalFormat
    {
        /// <summary>
        /// Major type 7 with additional information 28, followed by the sixteen raw bytes of the
        /// value. The historical form, and the default.
        /// </summary>
        /// <remarks>
        /// RFC 8949 §3.3 lists additional information 28-30 in major type 7 as reserved, so this
        /// occupies a slot the format has not assigned: it round-trips through this library and no
        /// other decoder reads it. Keep it for a contract whose only participants are Dahomey.Cbor,
        /// or while documents written by an earlier version are still in circulation; choose
        /// <see cref="DecimalFraction"/> for anything read outside this library.
        /// </remarks>
        DecimalFloat = 0,

        /// <summary>
        /// The RFC 8949 §3.4.4 decimal fraction: tag 4 over the two-element array
        /// <c>[exponent, mantissa]</c>, which is what every other CBOR implementation means by a
        /// decimal.
        /// </summary>
        /// <remarks>
        /// Carries every value the type holds, and its scale with it: a <see cref="decimal"/> of scale
        /// <c>s</c> and mantissa <c>m</c> is exactly the decimal fraction <c>[-s, m]</c>, so
        /// <c>0.00m</c> writes as <c>[-2, 0]</c> and <c>0m</c> as <c>[0, 0]</c> - the same distinction
        /// <see cref="DecimalFloat"/> keeps.
        /// <para>
        /// One exception, and it is in the encoding rather than in this library: a decimal fraction has
        /// no signed zero, so <c>-0.00m</c> reads back as <c>0.00m</c>. Equal by every comparison the
        /// language offers, and distinguishable only by <see cref="decimal.GetBits(decimal)"/> or by
        /// rendering it - where <see cref="DecimalFloat"/>, which stores the sign bit as it stands,
        /// keeps it.
        /// </para>
        /// </remarks>
        DecimalFraction = 1,
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

        /// <summary>
        /// Whether RFC 8746 typed arrays are read and written. Default <see cref="TypedArrayMode.Never"/>,
        /// which leaves both paths exactly as they were before typed arrays existed.
        /// </summary>
        /// <remarks>
        /// A typed array is one definite-length byte string, so <see cref="ArrayLengthMode"/> has nothing
        /// to apply to and is ignored for any array actually written as one.
        /// <para>
        /// Independent of <see cref="Deterministic"/> in both directions. A typed array and a plain CBOR
        /// array are two encodings of the same value, but which one is written is fixed by this setting
        /// rather than chosen per value, so either mode yields one encoding and §4.2.1 is satisfied
        /// without constraining this choice.
        /// </para>
        /// </remarks>
        public TypedArrayMode TypedArrayMode { get; set; } = TypedArrayMode.Never;

        /// <summary>
        /// Which encoding a <see cref="decimal"/> is written as. Default
        /// <see cref="DecimalFormat.DecimalFloat"/>, the non-standard form this library has always
        /// written, so no existing document moves.
        /// </summary>
        /// <remarks>
        /// A write-side setting only. Both forms are read whatever this says, and reading tag 4 is new
        /// capability rather than changed behaviour: it was a <see cref="CborException"/> before.
        /// <para>
        /// Independent of <see cref="Deterministic"/>. Either setting gives one <see cref="decimal"/>
        /// exactly one encoding - a decimal fraction's exponent and mantissa each use the shortest-form
        /// argument, and the mantissa reaches for a bignum tag only past 64 bits - so the bytes are
        /// determined either way, and §4.2.1 has nothing to say about which of the two to pick.
        /// </para>
        /// </remarks>
        public DecimalFormat DecimalFormat { get; set; }

        /// <summary>
        /// Semantic Tag to check if the discriminator is present when ObjectFormat is Array
        /// </summary>
        /// Default value is 39 (see: https://github.com/lucas-clemente/cbor-specs/blob/master/id.md)
        public ulong DiscriminatorSemanticTag { get; set; } = 39;

        /// <summary>
        /// Maximum nesting depth of maps and arrays, for both reading and writing. Default 64.
        /// </summary>
        /// <remarks>
        /// On write this converts a reference cycle - which CBOR cannot represent, and which would
        /// otherwise recurse until the stack is exhausted - into a <see cref="CborException"/>. On read
        /// it bounds stack use on untrusted input, where a handful of bytes can describe arbitrarily
        /// deep nesting. Raise it if the data is genuinely deeper than 64 levels.
        /// </remarks>
        public int MaxDepth { get; set; } = Serialization.CborWriter.DefaultMaxDepth;

        /// <summary>
        /// What reading a map does when the same key appears twice. Default
        /// <see cref="DuplicateKeyMode.Reject"/>, uniformly across every decode target.
        /// </summary>
        /// <remarks>
        /// Read once when a map or an object starts, not per key, and each read then runs to the end
        /// on the value it took. Settable at any point in an options object's life, including on the
        /// long-lived <see cref="Default"/>; a change made while a document is being read applies to
        /// the maps that start after it, so no single map is read half one way and half the other.
        /// </remarks>
        public DuplicateKeyMode DuplicateKeyMode { get; set; }

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