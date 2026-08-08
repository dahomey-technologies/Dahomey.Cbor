namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// What an <see cref="ObjectConverter{T}"/> holds against a member's key: the converter that reads
    /// it, and a small dense number identifying it within that converter.
    /// </summary>
    /// <remarks>
    /// The number lets <see cref="MemberReadState"/> hold "already read" as a bit rather than a set
    /// entry. It travels with the converter, in the same lookup, because the read path has already
    /// hashed the key to find one: asking a second structure for the number would put a second hash on
    /// every member of every object read, which is the cost the bitmask exists to avoid.
    /// <para>
    /// Numbered per converter and stored per converter, so nothing is assumed about member converter
    /// instances being unique to one <see cref="ObjectConverter{T}"/> - a number kept on the member
    /// converter itself would be silently wrong for one of two converters handed the same instance,
    /// and wrong here means a duplicate reported that is not one.
    /// </para>
    /// </remarks>
    public readonly struct MemberReadEntry
    {
        public MemberReadEntry(IMemberConverter converter, int ordinal)
        {
            Converter = converter;
            Ordinal = ordinal;
        }

        public IMemberConverter Converter { get; }

        /// <summary>
        /// This member's position in its converter's read set. Dense enough for a bitmask, and stable
        /// for the life of the converter; nothing depends on the value beyond its being distinct.
        /// </summary>
        public int Ordinal { get; }
    }
}
