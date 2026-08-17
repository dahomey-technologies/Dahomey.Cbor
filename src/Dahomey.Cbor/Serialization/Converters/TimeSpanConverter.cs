using System;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes a <see cref="TimeSpan"/> as an RFC 9581 §4 duration, tag 1002.
    /// </summary>
    /// <remarks>
    /// Registered only when <see cref="CborOptions.TimeSpanFormat"/> asks for it. Under the default
    /// <see cref="TimeSpanFormat.Members"/> there is no converter for a <see cref="TimeSpan"/> at all
    /// and the object mapping handles it, exactly as it always has -- which is what keeps this addition
    /// from changing any document already in circulation.
    /// </remarks>
    public class TimeSpanConverter : CborConverterBase<TimeSpan>
    {
        /// <summary>RFC 9581's tag for a duration.</summary>
        public const ulong DurationTag = 1002;

        /// <summary>The base value, in whole seconds. RFC 9581 §3's key 1, shared with tag 1001.</summary>
        private const int SecondsKey = 1;

        private const int MillisecondsKey = -3;
        private const int MicrosecondsKey = -6;
        private const int NanosecondsKey = -9;

        private const long NanosecondsPerTick = 100;

        public override TimeSpan Read(ref CborReader reader)
        {
            // A plain number is accepted as whole seconds. Not a form this writes, and not one RFC 9581
            // defines -- a duration is always a map there -- but it is the obvious shape for a peer that
            // never adopted the tag, and refusing it would buy nothing.
            switch (reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Signed:
                case CborDataItemType.Unsigned:
                    return TimeSpan.FromTicks(
                        checked(reader.ReadInt64() * TimeSpan.TicksPerSecond));

                case CborDataItemType.Double:
                case CborDataItemType.Single:
                    return TimeSpan.FromSeconds(reader.ReadDouble());
            }

            reader.ReadBeginMap();

            int size = reader.ReadSize();
            long ticks = 0;

            for (int read = 0; size == -1 || read < size; read++)
            {
                if (size == -1 && reader.IsBreak())
                {
                    break;
                }

                int key = reader.ReadInt32();

                switch (key)
                {
                    case SecondsKey:
                        ticks = checked(ticks + reader.ReadInt64() * TimeSpan.TicksPerSecond);
                        break;

                    case MillisecondsKey:
                        ticks = checked(ticks + reader.ReadInt64() * TimeSpan.TicksPerMillisecond);
                        break;

                    case MicrosecondsKey:
                        ticks = checked(ticks + reader.ReadInt64() * (TimeSpan.TicksPerMillisecond / 1000));
                        break;

                    case NanosecondsKey:
                        // TimeSpan resolves to 100ns, so anything finer is truncated rather than
                        // refused: a peer whose clock is more precise is interoperating correctly.
                        ticks = checked(ticks + reader.ReadInt64() / NanosecondsPerTick);
                        break;

                    default:
                        // RFC 9581 defines further keys -- a decimal or bigfloat base, a timescale, a
                        // clock quality -- none of which change the length this reads. Skipping keeps
                        // an unrecognised one from failing a document that is otherwise readable.
                        reader.SkipDataItem();
                        break;
                }
            }

            return TimeSpan.FromTicks(ticks);
        }

        public override void Write(ref CborWriter writer, TimeSpan value)
        {
            long ticks = value.Ticks;
            long seconds = ticks / TimeSpan.TicksPerSecond;

            // Truncation is toward zero on both sides, so the remainder carries the sign of the value
            // and the two components sum back to it without a case for negatives.
            long remainder = ticks % TimeSpan.TicksPerSecond;

            writer.WriteSemanticTag(DurationTag);

            if (remainder == 0)
            {
                writer.WriteBeginMap(1);
                writer.WriteInt32(SecondsKey);
                writer.WriteInt64(seconds);
                return;
            }

            writer.WriteBeginMap(2);
            writer.WriteInt32(SecondsKey);
            writer.WriteInt64(seconds);
            writer.WriteInt32(NanosecondsKey);
            writer.WriteInt64(remainder * NanosecondsPerTick);
        }
    }
}
