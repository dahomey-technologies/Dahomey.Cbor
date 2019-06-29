using Dahomey.Cbor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dahomey.Cbor.Serialization.Converters
{
    public class EnumConverter<T> : ICborConverter<T> where T : struct
    {
        private static readonly Func<int, int> intIdentity = x => x;
        private static readonly Func<int, T> intToEnum = (Func<int, T>)Delegate.CreateDelegate(typeof(Func<int, T>), null, intIdentity.Method);
        private static readonly Func<T, int> enumToInt = (Func<T, int>)Delegate.CreateDelegate(typeof(Func<T, int>), null, intIdentity.Method);

        private ByteBufferDictionary<T> names2Values = new ByteBufferDictionary<T>();
        private Dictionary<T, ReadOnlyMemory<byte>> values2Names;

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

        public T Read(ref CborReader reader)
        {
            if (reader.GetCurrentDataItemType() == CborDataItemType.Unsigned)
            {
                return ReadInt32(ref reader);
            }

            return ReadString(ref reader);
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
            return intToEnum((int)reader.ReadInt64());
        }

        public void Write(ref CborWriter writer, T value)
        {
            if (writer.Settings.EnumFormat == ValueFormat.WriteToString)
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
                writer.WriteInt64(enumToInt(value));
            }
        }

        public void WriteInt32(ref CborWriter writer, T value)
        {
            writer.WriteInt64(enumToInt(value));
        }
    }
}
