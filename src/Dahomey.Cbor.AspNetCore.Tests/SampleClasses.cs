using System;

namespace Dahomey.Cbor.AspNetCore.Tests
{
    public enum EnumTest
    {
        None = 0,
        Value1 = 1,
        Value2 = 2,
    }

    public class SimpleObject
    {
        public bool Boolean { get; set; }
        public sbyte SByte { get; set; }
        public byte Byte { get; set; }
        public ushort Int16 { get; set; }
        public short UInt16 { get; set; }
        public int Int32 { get; set; }
        public uint UInt32 { get; set; }
        public long Int64 { get; set; }
        public ulong UInt64 { get; set; }
        public string String { get; set; }
        public float Single { get; set; }
        public double Double { get; set; }
        public DateTime DateTime { get; set; }
        public EnumTest Enum { get; set; }
    }

}
