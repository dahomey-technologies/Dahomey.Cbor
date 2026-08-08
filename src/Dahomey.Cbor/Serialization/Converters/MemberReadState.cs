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
    /// Members are identified by an ordinal assigned per converter, so "already read" is a bit in a
    /// <see cref="ulong"/>: one test and one or per member. A type with more than 64 deserializable
    /// members falls back to a set, allocated only if a member past the 64th is actually read.
    /// </para>
    /// </remarks>
    public struct MemberReadState
    {
        /// <summary>
        /// Null when the type has no required members, which is what keeps the set off the common
        /// path. Not merged with the duplicate tracking below: this one holds member converters,
        /// because the required check compares against another converter's member list, and it is
        /// populated whatever <see cref="DuplicateKeyMode"/> is in force.
        /// </summary>
        private readonly HashSet<IMemberConverter>? _requiredMembersRead;

        private readonly bool _rejectDuplicates;

        /// <summary>Ordinals 0-63, one bit each.</summary>
        private ulong _seen;

        /// <summary>Ordinals 64 and above, for the rare type that has that many members.</summary>
        private HashSet<int>? _seenBeyond;

        public MemberReadState(bool trackRequiredMembers, bool rejectDuplicates)
        {
            _requiredMembersRead = trackRequiredMembers ? new HashSet<IMemberConverter>() : null;
            _rejectDuplicates = rejectDuplicates;
            _seen = 0;
            _seenBeyond = null;
        }

        /// <summary>
        /// Whether a member read twice is to be refused. Checked before the ordinal is looked up, so
        /// <see cref="DuplicateKeyMode.LastWins"/> pays nothing for the check being available.
        /// </summary>
        public readonly bool RejectsDuplicates => _rejectDuplicates;

        /// <summary>Whether required members are being tracked at all.</summary>
        public readonly bool TracksRequiredMembers => _requiredMembersRead != null;

        public readonly void MarkRequiredMemberRead(IMemberConverter memberConverter)
        {
            _requiredMembersRead?.Add(memberConverter);
        }

        public readonly bool WasRead(IMemberConverter memberConverter)
        {
            return _requiredMembersRead != null && _requiredMembersRead.Contains(memberConverter);
        }

        /// <summary>
        /// Records that the member with this ordinal has been read, and says whether it had been read
        /// already.
        /// </summary>
        /// <param name="ordinal">
        /// The member's position in its converter's read set, or -1 for a member that has none. A
        /// member with no ordinal is never reported as a repeat: saying "duplicate" about something
        /// this cannot actually tell apart would be worse than saying nothing.
        /// </param>
        public bool MarkSeen(int ordinal)
        {
            if (ordinal < 0)
            {
                return false;
            }

            if (ordinal < 64)
            {
                ulong bit = 1UL << ordinal;
                bool alreadySeen = (_seen & bit) != 0;
                _seen |= bit;
                return alreadySeen;
            }

            _seenBeyond ??= new HashSet<int>();
            return !_seenBeyond.Add(ordinal);
        }
    }
}
