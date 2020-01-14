using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class EnumConverter<T> : CborConverterBase<T> where T : struct
    {
        private readonly ByteBufferDictionary<T> names2Values = new ByteBufferDictionary<T>();
        private readonly Dictionary<T, ReadOnlyMemory<byte>> values2Names;

        public EnumConverter()
        {
            string[] names = Enum.GetNames(typeof(T));
            T[] values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();

            values2Names = new Dictionary<T, ReadOnlyMemory<byte>>(names.Length);

            for (int i = 0; i < names.Length; i++)
            {
                T value = values[i];
                ReadOnlyMemory<byte> name = Encoding.ASCII.GetBytes(names[i]);

                names2Values.Add(name.Span, value);
                values2Names[value] = name;
            }
        }

        public override T Read(ref CborReader reader)
        {
            switch(reader.GetCurrentDataItemType())
            {
                case CborDataItemType.Unsigned:
                case CborDataItemType.Signed:
                    return ReadInt32(ref reader);

                case CborDataItemType.String:
                    return ReadString(ref reader);

                default:
                    throw new NotSupportedException();
            }
        }

        public T ReadString(ref CborReader reader)
        {
            ReadOnlySpan<byte> rawName = reader.ReadRawString();
            if (!names2Values.TryGetValue(rawName, out T enumValue))
            {
                throw reader.BuildException($"Unknown name {Encoding.ASCII.GetString(rawName)} for enum {typeof(T).Name}");
            }

            return enumValue;
        }

        public T ReadInt32(ref CborReader reader)
        {
            int value = (int)reader.ReadInt64();
            return Unsafe.As<int, T>(ref value);
        }

        public override void Write(ref CborWriter writer, T value)
        {
            if (writer.Options.EnumFormat == ValueFormat.WriteToString)
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

        public void WriteInt32(ref CborWriter writer, T value)
        {
            writer.WriteInt64(Unsafe.As<T, int>(ref value));
        }
    }
}
