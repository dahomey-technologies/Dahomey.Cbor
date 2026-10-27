using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    /// <summary>
    /// Reads and writes an enum as its number or as its name.
    /// </summary>
    /// <remarks>
    /// A number is read and written in the enum's own underlying type, so a <c>long</c> or
    /// <c>ulong</c> enum keeps all 64 bits and a read outside the underlying type's range is a
    /// <see cref="CborException"/> rather than a wrapped value. A number that is in range but names no
    /// member is accepted: a <see cref="FlagsAttribute"/> combination has no member of its own, and a
    /// peer built against a newer enum may send a member this build has not heard of. Reading a text
    /// string accepts exactly the names the enum declares, whatever <see cref="CborOptions.EnumFormat"/>
    /// is set to. Names are written as UTF-8.
    /// </remarks>
    public class EnumConverter<T> : CborConverterBase<T> where T : struct
    {
        /// <summary>
        /// Which integer type the enum is declared over, resolved once per closed type so the JIT
        /// sees a constant and keeps only the arm that applies.
        /// </summary>
        private static readonly TypeCode UnderlyingTypeCode = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(T)));

        private readonly ByteBufferDictionary<T> names2Values = new ByteBufferDictionary<T>();
        private readonly Dictionary<T, ReadOnlyMemory<byte>> values2Names;

        private readonly CborOptions _options;

        public EnumConverter(CborOptions options)
        {
            _options = options;

            string[] names = Enum.GetNames(typeof(T));
            T[] values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();

            values2Names = new Dictionary<T, ReadOnlyMemory<byte>>(names.Length);

            for (int i = 0; i < names.Length; i++)
            {
                T value = values[i];
                ReadOnlyMemory<byte> name = Encoding.UTF8.GetBytes(names[i]);

                names2Values.Add(name.Span, value);
                values2Names[value] = name;
            }
        }

        public override T Read(ref CborReader reader)
        {
            CborDataItemType itemType = reader.GetCurrentDataItemType();

            switch (itemType)
            {
                case CborDataItemType.Unsigned:
                case CborDataItemType.Signed:
                    return ReadInt32(ref reader);

                case CborDataItemType.String:
                    return ReadString(ref reader);

                default:
                    throw reader.BuildException(
                        $"Cannot read a {itemType} as enum {typeof(T).Name}: expected an integer or a text string.");
            }
        }

        public T ReadString(ref CborReader reader)
        {
            ReadOnlySpan<byte> rawName = reader.ReadRawString();
            if (!names2Values.TryGetValue(rawName, out T enumValue))
            {
                throw reader.BuildException(
                    $"Unknown name {TextTruncation.Ellipsize(Encoding.UTF8.GetString(rawName))} for enum {typeof(T).Name}");
            }

            return enumValue;
        }

        /// <summary>
        /// Reads the enum's number in the enum's underlying type; the name is historical, and the
        /// underlying type is not always <c>int</c>.
        /// </summary>
        /// <exception cref="CborException">The number does not fit the underlying type.</exception>
        public T ReadInt32(ref CborReader reader)
        {
            switch (UnderlyingTypeCode)
            {
                case TypeCode.SByte:
                    {
                        sbyte value = reader.ReadSByte();
                        return Unsafe.As<sbyte, T>(ref value);
                    }

                case TypeCode.Byte:
                    {
                        byte value = reader.ReadByte();
                        return Unsafe.As<byte, T>(ref value);
                    }

                case TypeCode.Int16:
                    {
                        short value = reader.ReadInt16();
                        return Unsafe.As<short, T>(ref value);
                    }

                case TypeCode.UInt16:
                    {
                        ushort value = reader.ReadUInt16();
                        return Unsafe.As<ushort, T>(ref value);
                    }

                case TypeCode.Int32:
                    {
                        int value = reader.ReadInt32();
                        return Unsafe.As<int, T>(ref value);
                    }

                case TypeCode.UInt32:
                    {
                        uint value = reader.ReadUInt32();
                        return Unsafe.As<uint, T>(ref value);
                    }

                case TypeCode.Int64:
                    {
                        long value = reader.ReadInt64();
                        return Unsafe.As<long, T>(ref value);
                    }

                case TypeCode.UInt64:
                    {
                        ulong value = reader.ReadUInt64();
                        return Unsafe.As<ulong, T>(ref value);
                    }

                default:
                    throw UnsupportedUnderlyingType();
            }
        }

        public override void Write(ref CborWriter writer, T value)
        {
            if (_options.EnumFormat == ValueFormat.WriteToString)
            {
                WriteString(ref writer, value);
            }
            else
            {
                WriteInt32(ref writer, value);
            }
        }

        public void WriteString(ref CborWriter writer, T value)
        {
            if (values2Names.TryGetValue(value, out ReadOnlyMemory<byte> name))
            {
                writer.WriteString(name.Span);
            }
            else
            {
                WriteInt32(ref writer, value);
            }
        }

        /// <summary>
        /// Writes the enum's number in the enum's underlying type; the name is historical, and the
        /// underlying type is not always <c>int</c>.
        /// </summary>
        public void WriteInt32(ref CborWriter writer, T value)
        {
            switch (UnderlyingTypeCode)
            {
                case TypeCode.SByte:
                    writer.WriteSByte(Unsafe.As<T, sbyte>(ref value));
                    break;

                case TypeCode.Byte:
                    writer.WriteByte(Unsafe.As<T, byte>(ref value));
                    break;

                case TypeCode.Int16:
                    writer.WriteInt16(Unsafe.As<T, short>(ref value));
                    break;

                case TypeCode.UInt16:
                    writer.WriteUInt16(Unsafe.As<T, ushort>(ref value));
                    break;

                case TypeCode.Int32:
                    writer.WriteInt32(Unsafe.As<T, int>(ref value));
                    break;

                case TypeCode.UInt32:
                    writer.WriteUInt32(Unsafe.As<T, uint>(ref value));
                    break;

                case TypeCode.Int64:
                    writer.WriteInt64(Unsafe.As<T, long>(ref value));
                    break;

                case TypeCode.UInt64:
                    writer.WriteUInt64(Unsafe.As<T, ulong>(ref value));
                    break;

                default:
                    throw UnsupportedUnderlyingType();
            }
        }

        // C# cannot declare an enum over anything but the eight integer types above; IL can (char,
        // bool, native integers), and those have no number this converter could read or write.
        private static NotSupportedException UnsupportedUnderlyingType()
        {
            return new NotSupportedException(
                $"Enum {typeof(T).Name} is declared over {UnderlyingTypeCode}, which has no CBOR number form here.");
        }
    }
}
