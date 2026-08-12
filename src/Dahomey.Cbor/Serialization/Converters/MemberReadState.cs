using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// What the document has supplied so far, for the members of the one object being read: which
    /// required members have arrived, and - when <see cref="DuplicateKeyMode.Reject"/> is in force -
    /// which members have already been read once, so that a repeat can be refused.
    /// </summary>
    /// <remarks>
    /// A mutable struct, held in the reader context and passed by <c>ref</c> throughout, so that
    /// neither piece of state costs an allocation on the path taken by a document with no duplicates
    /// and no required members - which is most documents.
    /// <para>
    /// Members are identified by the ordinal their <see cref="MemberReadEntry"/> carries, so "already
    /// read" is a bit in a <see cref="ulong"/>: one test and one or per member, with nothing to look
    /// up, since the read path has the entry in hand already. A type with more than 64 deserializable
    /// members falls back to a set, allocated only if a member past the 64th is actually read.
    /// </para>
    /// </remarks>
    public struct MemberReadState
    {
        /// <summary>
        /// Null while nothing requires tracking - neither the declared type nor, once a discriminator
        /// has settled it, the resolved one - which is what keeps the set off the common path. Not
        /// merged with the duplicate tracking below: this one holds member converters,
        /// because the required check compares against another converter's member list, and it is
        /// populated whatever <see cref="DuplicateKeyMode"/> is in force.
        /// </summary>
        private HashSet<IMemberConverter>? _requiredMembersRead;

        private readonly bool _rejectDuplicates;

        /// <summary>Ordinals 0-63, one bit each.</summary>
        private ulong _seen;

        /// <summary>Ordinals 64 and above, for the rare type that has that many members.</summary>
        private HashSet<int>? _seenBeyond;

        /// <summary>
        /// The discriminator, which has no ordinal because it is not a member of the type: it names
        /// the type rather than carrying a value, and is read by the convention rather than by a
        /// member converter. It is still a key of the map, so a document repeating it is refused like
        /// any other repeat.
        /// </summary>
        private bool _discriminatorRead;

        public MemberReadState(bool trackRequiredMembers, bool rejectDuplicates)
        {
            _requiredMembersRead = trackRequiredMembers ? new HashSet<IMemberConverter>() : null;
            _rejectDuplicates = rejectDuplicates;
            _seen = 0;
            _seenBeyond = null;
            _discriminatorRead = false;
        }

        /// <summary>
        /// The mode this read is running under. Taken once, when the read started, and answered from
        /// here rather than re-read from the options, so that every path of one object read agrees on
        /// the policy even if the options change underneath it.
        /// </summary>
        public readonly DuplicateKeyMode Mode
            => _rejectDuplicates ? DuplicateKeyMode.Reject : DuplicateKeyMode.LastWins;

        /// <summary>Whether required members are being tracked at all.</summary>
        public readonly bool TracksRequiredMembers => _requiredMembersRead != null;

        /// <summary>
        /// Turns tracking on for a read that started without it.
        /// </summary>
        /// <remarks>
        /// The constructor can only consult the <em>declared</em> type's required members, and on a
        /// polymorphic read the list that is checked is the resolved type's - a type deriving from a
        /// base that requires nothing may require something of its own. Called once the discriminator
        /// has settled which converter this object is being read by, which is before its first member
        /// is read, so nothing can already have been missed.
        /// <para>
        /// Idempotent, and never turns tracking off: a declared type's requirements are the derived
        /// type's as well, so the set only ever needs to start existing.
        /// </para>
        /// </remarks>
        public void TrackRequiredMembers()
        {
            _requiredMembersRead ??= new HashSet<IMemberConverter>();
        }

        public readonly bool WasRead(IMemberConverter memberConverter)
        {
            return _requiredMembersRead != null && _requiredMembersRead.Contains(memberConverter);
        }

        /// <summary>
        /// Records that the document has supplied this member, and says whether it had supplied it
        /// once already - which is a duplicate map key, for a caller that means to refuse one.
        /// </summary>
        /// <remarks>
        /// Always false under <see cref="DuplicateKeyMode.LastWins"/>, where nothing is going to be
        /// refused: the bookkeeping is skipped rather than done and discarded. Also false for an entry
        /// identifying no member - see <see cref="MemberReadEntry.Ordinal"/> - since saying "duplicate"
        /// about something unidentified would be worse than saying nothing.
        /// </remarks>
        public bool MarkRead(in MemberReadEntry entry)
        {
            _requiredMembersRead?.Add(entry.Converter);

            if (!_rejectDuplicates || entry.Ordinal <= 0)
            {
                return false;
            }

            int index = entry.Ordinal - 1;

            if (index < 64)
            {
                ulong bit = 1UL << index;
                bool alreadyRead = (_seen & bit) != 0;
                _seen |= bit;
                return alreadyRead;
            }

            _seenBeyond ??= new HashSet<int>();
            return !_seenBeyond.Add(index);
        }

        /// <inheritdoc cref="MarkRead(in MemberReadEntry)"/>
        public bool MarkDiscriminatorRead()
        {
            if (!_rejectDuplicates)
            {
                return false;
            }

            bool alreadyRead = _discriminatorRead;
            _discriminatorRead = true;
            return alreadyRead;
        }
    }
}
