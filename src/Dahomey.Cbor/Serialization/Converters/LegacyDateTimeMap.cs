#if NET6_0_OR_GREATER
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads the map that earlier versions wrote for a <see cref="DateOnly"/> or a
    /// <see cref="TimeOnly"/>, when neither had a converter and each was written as an object of its
    /// public properties.
    /// </summary>
    /// <remarks>
    /// Only the integer members are kept, keyed case-insensitively so a naming convention does not
    /// matter; everything else (<c>DayOfWeek</c>, <c>DayOfYear</c>, ...) is skipped, since the
    /// remaining members are derived from the ones that are read.
    /// </remarks>
    internal sealed class LegacyDateTimeMap : ICborMapReader<LegacyDateTimeMap.Context>
    {
        private static readonly LegacyDateTimeMap _rejectDuplicates = new LegacyDateTimeMap(true);
        private static readonly LegacyDateTimeMap _lastWins = new LegacyDateTimeMap(false);

        private readonly bool _reject;

        private LegacyDateTimeMap(bool reject)
        {
            _reject = reject;
        }

        public struct Context
        {
            internal Dictionary<string, long> Members;
            internal HashSet<string>? Seen;
        }

        /// <summary>
        /// Reads the map, honouring <see cref="CborOptions.DuplicateKeyMode"/> exactly as a mapped
        /// class does: a repeated key is refused under <see cref="DuplicateKeyMode.Reject"/>, and the
        /// last one wins otherwise.
        /// </summary>
        public static Dictionary<string, long> Read(ref CborReader reader, CborOptions options)
        {
            Context context = default;
            reader.ReadMap(
                options.DuplicateKeyMode == DuplicateKeyMode.LastWins ? _lastWins : _rejectDuplicates,
                ref context);
            return context.Members;
        }

        void ICborMapReader<Context>.ReadBeginMap(int size, ref Context context)
        {
            context.Members = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            // Only tracked when a repeat is going to be refused; under LastWins the dictionary's
            // indexer overwrites and nothing needs remembering.
            context.Seen = _reject ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
        }

        void ICborMapReader<Context>.ReadMapItem(ref CborReader reader, ref Context context)
        {
            // The key is always consumed, so the parent keeps its place whatever the map holds. These
            // maps only ever had the property names as keys.
            if (reader.GetCurrentDataItemType() != CborDataItemType.String)
            {
                throw reader.BuildException("A date or time map has only text keys");
            }

            string key = reader.ReadString() ?? string.Empty;

            if (_reject && !context.Seen!.Add(key))
            {
                throw reader.BuildException(MapKeyErrors.Duplicate(key));
            }

            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    context.Members[key] = reader.ReadInt64();
                    break;

                default:
                    // Not a member this reader uses. Under LastWins it still replaces an earlier
                    // value for the same key, which then no longer counts.
                    context.Members.Remove(key);
                    reader.SkipDataItem();
                    break;
            }
        }
    }
}
#endif
