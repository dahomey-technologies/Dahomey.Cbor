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
        public static readonly LegacyDateTimeMap Instance = new LegacyDateTimeMap();

        public struct Context
        {
            internal Dictionary<string, long> Members;
        }

        public static Dictionary<string, long> Read(ref CborReader reader)
        {
            Context context = default;
            reader.ReadMap(Instance, ref context);
            return context.Members;
        }

        void ICborMapReader<Context>.ReadBeginMap(int size, ref Context context)
        {
            context.Members = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        void ICborMapReader<Context>.ReadMapItem(ref CborReader reader, ref Context context)
        {
            string? key = reader.GetCurrentDataItemType() == CborDataItemType.String
                ? reader.ReadString()
                : null;

            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    long value = reader.ReadInt64();

                    if (key != null)
                    {
                        context.Members[key] = value;
                    }

                    break;

                default:
                    reader.SkipDataItem();
                    break;
            }
        }
    }
}
#endif
