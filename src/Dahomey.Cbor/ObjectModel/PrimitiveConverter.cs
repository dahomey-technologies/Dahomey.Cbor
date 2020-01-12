
using System;
using System.Collections.Generic;

namespace Dahomey.Cbor.ObjectModel
{
    internal static class PrimitiveConverter
    {
        private static Dictionary<(Type, Type), Delegate> _converters = new Dictionary<(Type, Type), Delegate>()
        {
            [(typeof(bool), typeof(bool))] = (Func<bool, bool>)(v => v),
            [(typeof(string), typeof(string))] = (Func<string, string>)(v => v),
            [(typeof(sbyte), typeof(long))] = (Func<sbyte, long>)(v => v),
            [(typeof(sbyte), typeof(ulong))] = (Func<sbyte, ulong>)(v => (ulong)v),
            [(typeof(sbyte), typeof(double))] = (Func<sbyte, double>)(v => v),
            [(typeof(sbyte), typeof(float))] = (Func<sbyte, float>)(v => v),
            [(typeof(byte), typeof(long))] = (Func<byte, long>)(v => v),
            [(typeof(byte), typeof(ulong))] = (Func<byte, ulong>)(v => v),
            [(typeof(byte), typeof(double))] = (Func<byte, double>)(v => v),
            [(typeof(byte), typeof(float))] = (Func<byte, float>)(v => v),
            [(typeof(short), typeof(long))] = (Func<short, long>)(v => v),
            [(typeof(short), typeof(ulong))] = (Func<short, ulong>)(v => (ulong)v),
            [(typeof(short), typeof(double))] = (Func<short, double>)(v => v),
            [(typeof(short), typeof(float))] = (Func<short, float>)(v => v),
            [(typeof(ushort), typeof(long))] = (Func<ushort, long>)(v => v),
            [(typeof(ushort), typeof(ulong))] = (Func<ushort, ulong>)(v => v),
            [(typeof(ushort), typeof(double))] = (Func<ushort, double>)(v => v),
            [(typeof(ushort), typeof(float))] = (Func<ushort, float>)(v => v),
            [(typeof(int), typeof(long))] = (Func<int, long>)(v => v),
            [(typeof(int), typeof(ulong))] = (Func<int, ulong>)(v => (ulong)v),
            [(typeof(int), typeof(double))] = (Func<int, double>)(v => v),
            [(typeof(int), typeof(float))] = (Func<int, float>)(v => v),
            [(typeof(uint), typeof(long))] = (Func<uint, long>)(v => v),
            [(typeof(uint), typeof(ulong))] = (Func<uint, ulong>)(v => v),
            [(typeof(uint), typeof(double))] = (Func<uint, double>)(v => v),
            [(typeof(uint), typeof(float))] = (Func<uint, float>)(v => v),
            [(typeof(long), typeof(sbyte))] = (Func<long, sbyte>)(v => (sbyte)v),
            [(typeof(long), typeof(byte))] = (Func<long, byte>)(v => (byte)v),
            [(typeof(long), typeof(short))] = (Func<long, short>)(v => (short)v),
            [(typeof(long), typeof(ushort))] = (Func<long, ushort>)(v => (ushort)v),
            [(typeof(long), typeof(int))] = (Func<long, int>)(v => (int)v),
            [(typeof(long), typeof(uint))] = (Func<long, uint>)(v => (uint)v),
            [(typeof(long), typeof(long))] = (Func<long, long>)(v => v),
            [(typeof(long), typeof(ulong))] = (Func<long, ulong>)(v => (ulong)v),
            [(typeof(long), typeof(double))] = (Func<long, double>)(v => v),
            [(typeof(long), typeof(float))] = (Func<long, float>)(v => v),
            [(typeof(long), typeof(decimal))] = (Func<long, decimal>)(v => v),
            [(typeof(ulong), typeof(sbyte))] = (Func<ulong, sbyte>)(v => (sbyte)v),
            [(typeof(ulong), typeof(byte))] = (Func<ulong, byte>)(v => (byte)v),
            [(typeof(ulong), typeof(short))] = (Func<ulong, short>)(v => (short)v),
            [(typeof(ulong), typeof(ushort))] = (Func<ulong, ushort>)(v => (ushort)v),
            [(typeof(ulong), typeof(int))] = (Func<ulong, int>)(v => (int)v),
            [(typeof(ulong), typeof(uint))] = (Func<ulong, uint>)(v => (uint)v),
            [(typeof(ulong), typeof(long))] = (Func<ulong, long>)(v => (long)v),
            [(typeof(ulong), typeof(ulong))] = (Func<ulong, ulong>)(v => v),
            [(typeof(ulong), typeof(double))] = (Func<ulong, double>)(v => v),
            [(typeof(ulong), typeof(float))] = (Func<ulong, float>)(v => v),
            [(typeof(ulong), typeof(decimal))] = (Func<ulong, decimal>)(v => v),
            [(typeof(decimal), typeof(double))] = (Func<decimal, double>)(v => (double)v),
            [(typeof(decimal), typeof(float))] = (Func<decimal, float>)(v => (float)v),
            [(typeof(double), typeof(sbyte))] = (Func<double, sbyte>)(v => (sbyte)v),
            [(typeof(double), typeof(byte))] = (Func<double, byte>)(v => (byte)v),
            [(typeof(double), typeof(short))] = (Func<double, short>)(v => (short)v),
            [(typeof(double), typeof(ushort))] = (Func<double, ushort>)(v => (ushort)v),
            [(typeof(double), typeof(int))] = (Func<double, int>)(v => (int)v),
            [(typeof(double), typeof(uint))] = (Func<double, uint>)(v => (uint)v),
            [(typeof(double), typeof(long))] = (Func<double, long>)(v => (long)v),
            [(typeof(double), typeof(ulong))] = (Func<double, ulong>)(v => (ulong)v),
            [(typeof(double), typeof(double))] = (Func<double, double>)(v => v),
            [(typeof(double), typeof(float))] = (Func<double, float>)(v => (float)v),
            [(typeof(double), typeof(decimal))] = (Func<double, decimal>)(v => (decimal)v),
            [(typeof(float), typeof(sbyte))] = (Func<float, sbyte>)(v => (sbyte)v),
            [(typeof(float), typeof(byte))] = (Func<float, byte>)(v => (byte)v),
            [(typeof(float), typeof(short))] = (Func<float, short>)(v => (short)v),
            [(typeof(float), typeof(ushort))] = (Func<float, ushort>)(v => (ushort)v),
            [(typeof(float), typeof(int))] = (Func<float, int>)(v => (int)v),
            [(typeof(float), typeof(uint))] = (Func<float, uint>)(v => (uint)v),
            [(typeof(float), typeof(long))] = (Func<float, long>)(v => (long)v),
            [(typeof(float), typeof(ulong))] = (Func<float, ulong>)(v => (ulong)v),
            [(typeof(float), typeof(float))] = (Func<float, float>)(v => v),
            [(typeof(float), typeof(double))] = (Func<float, double>)(v => v),
            [(typeof(float), typeof(decimal))] = (Func<float, decimal>)(v => (decimal)v),
        };

        private static class Cache<T1, T2>
        {
            public readonly static Func<T1, T2> Converter = (Func<T1, T2>)_converters[(typeof(T1), typeof(T2))];
        }

        public static Func<T1, T2> Get<T1, T2>() => Cache<T1, T2>.Converter;
    }

    internal struct Primitive<T1, T2>
    {
        public static Func<T1, T2> Converter => PrimitiveConverter.Get<T1, T2>();
    }
}
