using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Dahomey.Cbor.Util
{
    // Temporarily implements missing APIs for System.Half
    // Remove class once https://github.com/dotnet/runtime/issues/38288 has been addressed
    internal static class HalfHelpers
    {
        public const int SizeOfHalf = sizeof(short);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Half ReadHalf(ReadOnlySpan<byte> source)
        {
            if (BitConverter.IsLittleEndian)
            {
                ushort value = BinaryPrimitives.ReadUInt16BigEndian(source);
                return UInt16BitsToHalf(value);
            }
            else
            {
                return UInt16BitsToHalf(MemoryMarshal.Read<ushort>(source));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteHalf(Span<byte> destination, Half value)
        {
            if (BitConverter.IsLittleEndian)
            {
                BinaryPrimitives.WriteUInt16BigEndian(destination, HalfToUInt16Bits(value));
            }
            else
            {
                ushort tmp = HalfToUInt16Bits(value);
                MemoryMarshal.Write(destination, ref tmp);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ushort HalfToUInt16Bits(Half value)
        {
            return *((ushort*)&value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Half UInt16BitsToHalf(ushort value)
        {
            return *(Half*)&value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int SingleToInt32Bits(float value)
        {
#if NET5_0
            return BitConverter.SingleToInt32Bits(value);
#else
            return *((int*)&value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float Int32BitsToSingle(int value)
        {
#if NET5_0
            return BitConverter.Int32BitsToSingle(value);
#else
            return *((float*)&value);
#endif
        }

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value)
        {
            return 31 ^ Log2SoftwareFallback(value);
        }

        private static ReadOnlySpan<byte> Log2DeBruijn => new byte[32]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31
        };

        /// <summary>
        /// Returns the integer (floor) log of the specified value, base 2.
        /// Note that by convention, input value 0 returns 0 since Log(0) is undefined.
        /// Does not directly use any hardware intrinsics, nor does it incur branching.
        /// </summary>
        /// <param name="value">The value.</param>
        private static int Log2SoftwareFallback(uint value)
        {
            // No AggressiveInlining due to large method size
            // Has conventional contract 0->0 (Log(0) is undefined)

            // Fill trailing zeros with ones, eg 00010010 becomes 00011111
            value |= value >> 01;
            value |= value >> 02;
            value |= value >> 04;
            value |= value >> 08;
            value |= value >> 16;

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return Unsafe.AddByteOffset(
                // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_1100_0100_1010_1100_1101_1101u
                ref MemoryMarshal.GetReference(Log2DeBruijn),
                // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
                (IntPtr)(int)((value * 0x07C4ACDDu) >> 27));
        }
    }
}
