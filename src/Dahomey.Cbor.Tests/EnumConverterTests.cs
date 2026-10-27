using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Util;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// An enum is a number of its own underlying type or a name. The number keeps every bit of that
    /// type, a number the type cannot hold is a CborException, and anything that is neither a number
    /// nor a name is a CborException carrying the path, as it is for an int member.
    /// </summary>
    public class EnumConverterTests
    {
        public enum SByteEnum : sbyte { Min = sbyte.MinValue, MinusOne = -1, Zero = 0, One = 1, Max = sbyte.MaxValue }
        public enum ByteEnum : byte { Zero = 0, One = 1, Max = byte.MaxValue }
        public enum ShortEnum : short { Min = short.MinValue, MinusOne = -1, Zero = 0, One = 1, Max = short.MaxValue }
        public enum UShortEnum : ushort { Zero = 0, One = 1, Max = ushort.MaxValue }
        public enum IntEnum : int { Min = int.MinValue, MinusOne = -1, Zero = 0, One = 1, Max = int.MaxValue }
        public enum UIntEnum : uint { Zero = 0, One = 1, Max = uint.MaxValue }
        public enum LongEnum : long { Min = long.MinValue, MinusOne = -1, Zero = 0, One = 1, Big = 5_000_000_000, Max = long.MaxValue }
        public enum ULongEnum : ulong { Zero = 0, One = 1, Big = 5_000_000_000, High = 1UL << 63, Max = ulong.MaxValue }

        [Flags]
        public enum Colours : byte { None = 0, Red = 1, Green = 2, Blue = 4 }

        [Flags]
        public enum Wide : ulong { None = 0, Low = 1, High = 1UL << 63 }

        // Four letters of the Latin-1 supplement. An ASCII encoding folds the two accented pairs onto
        // the same name, and onto "?".
        public enum Accented { Größe = 1, Straße = 2, GrößenÄnderung = 3 }
        public enum Folded { É = 1, È = 2 }

        public class Holder
        {
            public IntEnum En { get; set; }
        }

        public class NullableHolder
        {
            public IntEnum? En { get; set; }
        }

        public class IntHolder
        {
            public int En { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class KeyedHolder
        {
            [CborProperty(1)]
            public IntEnum En { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        public class KeyedIntHolder
        {
            [CborProperty(1)]
            public int En { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class PositionalHolder
        {
            [CborProperty(0)]
            public IntEnum En { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        public class PositionalIntHolder
        {
            [CborProperty(0)]
            public int En { get; set; }
        }

        private static readonly CborOptions ToStringOptions = new CborOptions { EnumFormat = ValueFormat.WriteToString };

        private static readonly (Type Type, BigInteger Min, BigInteger Max)[] Underlying =
        {
            (typeof(SByteEnum), sbyte.MinValue, sbyte.MaxValue),
            (typeof(ByteEnum), byte.MinValue, byte.MaxValue),
            (typeof(ShortEnum), short.MinValue, short.MaxValue),
            (typeof(UShortEnum), ushort.MinValue, ushort.MaxValue),
            (typeof(IntEnum), int.MinValue, int.MaxValue),
            (typeof(UIntEnum), uint.MinValue, uint.MaxValue),
            (typeof(LongEnum), long.MinValue, long.MaxValue),
            (typeof(ULongEnum), ulong.MinValue, ulong.MaxValue),
        };

        // Every CBOR integer, whatever the enum, so a value is tried against each type that cannot hold
        // it as well as against the one whose bound it sits on.
        private static IEnumerable<BigInteger> Wide64()
        {
            yield return BigInteger.Pow(2, 31);
            yield return BigInteger.Pow(2, 32);
            yield return BigInteger.Pow(2, 32) + 1;
            yield return BigInteger.Pow(2, 63) - 1;
            yield return BigInteger.Pow(2, 63);
            yield return BigInteger.Pow(2, 64) - 1;
            yield return -BigInteger.Pow(2, 31) - 1;
            yield return -BigInteger.Pow(2, 63);
            yield return -BigInteger.Pow(2, 63) - 1;
            yield return -BigInteger.Pow(2, 64);
        }

        private static IEnumerable<BigInteger> Boundaries(BigInteger min, BigInteger max)
        {
            return new[] { min - 1, min, min + 1, -1, 0, 1, max - 1, max, max + 1 }
                .Concat(Wide64())
                // CBOR integers stop at -2^64 and 2^64 - 1; min - 1 and max + 1 of a 64-bit type can
                // step past that.
                .Where(v => v >= -BigInteger.Pow(2, 64) && v < BigInteger.Pow(2, 64))
                .Distinct();
        }

        public static IEnumerable<object[]> Grid()
        {
            foreach ((Type type, BigInteger min, BigInteger max) in Underlying)
            {
                foreach (BigInteger value in Boundaries(min, max))
                {
                    yield return new object[] { type, value.ToString(CultureInfo.InvariantCulture) };
                }
            }
        }

        public static IEnumerable<object[]> FittingGrid()
        {
            foreach ((Type type, BigInteger min, BigInteger max) in Underlying)
            {
                foreach (BigInteger value in Boundaries(min, max).Where(v => v >= min && v <= max))
                {
                    yield return new object[] { type, value.ToString(CultureInfo.InvariantCulture) };
                }
            }
        }

        public static IEnumerable<object[]> RefusedGrid()
        {
            foreach ((Type type, BigInteger min, BigInteger max) in Underlying)
            {
                foreach (BigInteger value in Boundaries(min, max).Where(v => v < min || v > max))
                {
                    yield return new object[] { type, value.ToString(CultureInfo.InvariantCulture) };
                }
            }
        }

        private static (BigInteger Min, BigInteger Max) RangeOf(Type enumType)
        {
            var range = Underlying.Single(u => u.Type == enumType);
            return (range.Min, range.Max);
        }

        // Shortest-form CBOR integer, written out of RFC 8949 section 3.1 rather than out of the
        // library, so the library is compared against the specification and not against itself.
        private static byte[] Encode(BigInteger value)
        {
            bool negative = value.Sign < 0;
            ulong argument = (ulong)(negative ? -1 - value : value);
            byte major = (byte)(negative ? 0x20 : 0x00);

            if (argument < 24)
            {
                return new[] { (byte)(major | argument) };
            }

            int length = argument <= 0xFF ? 1 : argument <= 0xFFFF ? 2 : argument <= 0xFFFFFFFF ? 4 : 8;
            byte initial = (byte)(major | (length == 1 ? 24 : length == 2 ? 25 : length == 4 ? 26 : 27));
            byte[] bytes = new byte[1 + length];
            bytes[0] = initial;

            for (int i = 0; i < length; i++)
            {
                bytes[1 + i] = (byte)(argument >> (8 * (length - 1 - i)));
            }

            return bytes;
        }

        private static object EnumOf(Type enumType, BigInteger value)
        {
            return value.Sign < 0
                ? Enum.ToObject(enumType, (long)value)
                : Enum.ToObject(enumType, (ulong)value);
        }

        private static BigInteger NumberOf(object enumValue)
        {
            return BigInteger.Parse(Enum.Format(enumValue.GetType(), enumValue, "D"), CultureInfo.InvariantCulture);
        }

        private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);

        private static byte[] Bytes(string hex) => Convert.FromHexString(hex);

        private static string Write(Type type, object value, CborOptions options = null)
        {
            using (ByteBufferWriter writer = new ByteBufferWriter())
            {
                Cbor.Serialize(value, type, writer, options);
                return Hex(writer.WrittenSpan.ToArray());
            }
        }

        private static CborException Refusal(Type type, byte[] buffer, CborOptions options = null)
        {
            try
            {
                Cbor.Deserialize(type, buffer, options);
            }
            catch (CborException e)
            {
                return e;
            }

            return null;
        }

        private static CborException Refusal<T>(byte[] buffer, CborOptions options = null)
        {
            return Refusal(typeof(T), buffer, options);
        }

        private static bool SystemTextJsonReads(Type enumType, string number)
        {
            try
            {
                JsonSerializer.Deserialize(number, enumType);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------------------------------
        // The number, over the whole range of every underlying type

        /// <summary>
        /// A number reads exactly when the underlying type holds it, and then reads as itself. The
        /// same verdict System.Text.Json gives for the same number, which is checked alongside so
        /// that "the range" is not only this file's opinion.
        /// </summary>
        [Theory]
        [MemberData(nameof(Grid))]
        public void ANumberReadsExactlyWhenTheUnderlyingTypeHoldsIt(Type enumType, string text)
        {
            BigInteger value = BigInteger.Parse(text, CultureInfo.InvariantCulture);
            (BigInteger min, BigInteger max) = RangeOf(enumType);
            bool fits = value >= min && value <= max;

            Assert.Equal(fits, SystemTextJsonReads(enumType, text));

            byte[] buffer = Encode(value);

            if (fits)
            {
                object actual = Cbor.Deserialize(enumType, buffer);

                Assert.IsType(enumType, actual);
                Assert.Equal(value, NumberOf(actual));
            }
            else
            {
                CborException exception = Refusal(enumType, buffer);

                Assert.NotNull(exception);
                Assert.False(string.IsNullOrEmpty(exception.Message));
            }
        }

        [Theory]
        [MemberData(nameof(RefusedGrid))]
        public void AnOutOfRangeNumberInAMemberIsRefusedWithItsPath(Type enumType, string text)
        {
            BigInteger value = BigInteger.Parse(text, CultureInfo.InvariantCulture);

            // A map {"En": <number>} read as the member of an object whose member is this enum.
            Type holder = typeof(GenericHolder<>).MakeGenericType(enumType);
            byte[] buffer = new byte[] { 0xA1, 0x62, (byte)'E', (byte)'n' }.Concat(Encode(value)).ToArray();

            CborException exception = Refusal(holder, buffer);

            Assert.NotNull(exception);
            Assert.Equal("$.En", exception.Path);
        }

        public class GenericHolder<TEnum> where TEnum : struct
        {
            public TEnum En { get; set; }
        }

        /// <summary>
        /// The number is written in the underlying type's full width and sign -- byte for byte what
        /// the specification says -- under both formats, since an enum written by name still falls
        /// back to the number for a value it has no name for.
        /// </summary>
        [Theory]
        [MemberData(nameof(FittingGrid))]
        public void ANumberWritesAtTheFullWidthOfTheUnderlyingType(Type enumType, string text)
        {
            BigInteger value = BigInteger.Parse(text, CultureInfo.InvariantCulture);
            object enumValue = EnumOf(enumType, value);
            string expected = Hex(Encode(value));

            // System.Text.Json writes an enum as its number, so this is also its answer.
            Assert.Equal(text, JsonSerializer.Serialize(enumValue, enumType));

            Assert.Equal(expected, Write(enumType, enumValue));

            if (!Enum.IsDefined(enumType, enumValue))
            {
                Assert.Equal(expected, Write(enumType, enumValue, ToStringOptions));
            }
        }

        [Theory]
        [MemberData(nameof(FittingGrid))]
        public void ANumberRoundTripsUnderBothFormats(Type enumType, string text)
        {
            BigInteger value = BigInteger.Parse(text, CultureInfo.InvariantCulture);
            object enumValue = EnumOf(enumType, value);

            foreach (CborOptions options in new[] { new CborOptions(), ToStringOptions })
            {
                string hex = Write(enumType, enumValue, options);

                Assert.Equal(enumValue, Cbor.Deserialize(enumType, Bytes(hex), options));
            }
        }

        // The cases the audit measured, as written down in the issue.
        [Theory]
        [InlineData(typeof(IntEnum), "1B0000000100000001")]    // 4294967297, 2^32 + 1; used to read as 1
        [InlineData(typeof(IntEnum), "1B000000012A05F200")]    // 5000000000; used to read as 705032704
        [InlineData(typeof(IntEnum), "3B7FFFFFFFFFFFFFFF")]    // -2^63; used to read as 0
        [InlineData(typeof(IntEnum), "1A80000000")]            // 2^31; used to read as int.MinValue
        [InlineData(typeof(IntEnum), "3A80000000")]            // -2^31 - 1
        [InlineData(typeof(ByteEnum), "19012C")]               // 300; used to read as 44
        [InlineData(typeof(ByteEnum), "190100")]               // 256
        [InlineData(typeof(ByteEnum), "20")]                   // -1; used to read as 255
        [InlineData(typeof(UIntEnum), "20")]                   // -1; used to read as 4294967295
        [InlineData(typeof(UIntEnum), "1B0000000100000000")]   // 2^32
        [InlineData(typeof(SByteEnum), "1880")]                // 128
        [InlineData(typeof(SByteEnum), "3880")]                // -129
        [InlineData(typeof(LongEnum), "1B8000000000000000")]   // 2^63
        [InlineData(typeof(LongEnum), "3B8000000000000000")]   // -2^63 - 1
        [InlineData(typeof(ULongEnum), "20")]                  // -1
        public void ANumberTheTypeCannotHoldIsACborException(Type enumType, string hex)
        {
            Assert.NotNull(Refusal(enumType, Bytes(hex)));
        }

        [Theory]
        [InlineData(typeof(LongEnum), "01", 1L)]                              // used to read as garbage
        [InlineData(typeof(LongEnum), "00", 0L)]                              // so did zero
        [InlineData(typeof(LongEnum), "1B000000012A05F200", 5_000_000_000L)]
        [InlineData(typeof(LongEnum), "20", -1L)]
        [InlineData(typeof(LongEnum), "3B7FFFFFFFFFFFFFFF", long.MinValue)]
        [InlineData(typeof(IntEnum), "3A7FFFFFFF", int.MinValue)]
        [InlineData(typeof(ByteEnum), "18FF", 255L)]
        [InlineData(typeof(SByteEnum), "387F", -128L)]
        public void ANumberTheTypeHoldsReadsAsItself(Type enumType, string hex, long expected)
        {
            object actual = Cbor.Deserialize(enumType, Bytes(hex));

            Assert.Equal(new BigInteger(expected), NumberOf(actual));
        }

        /// <summary>
        /// The span, the sequence and the fragmented-sequence readers all land on the same value.
        /// </summary>
        [Fact]
        public void EveryReaderAgrees()
        {
            Assert.Equal(LongEnum.Big, Helper.Read<LongEnum>("1B000000012A05F200"));
            Assert.Equal(LongEnum.Min, Helper.Read<LongEnum>("3B7FFFFFFFFFFFFFFF"));
            Assert.Equal(ULongEnum.Max, Helper.Read<ULongEnum>("1BFFFFFFFFFFFFFFFF"));
            Assert.Equal(SByteEnum.Min, Helper.Read<SByteEnum>("387F"));
            Assert.Equal(Accented.Größe, Helper.Read<Accented>("674772C3B6C39F65"));
        }

        [Fact]
        public void ASemanticTagBeforeTheNumberIsSkipped()
        {
            Assert.Equal(LongEnum.Big, Cbor.Deserialize<LongEnum>(Bytes("C11B000000012A05F200")));
        }

        [Fact]
        public void AUlongEnumReadsAboveLongMaxValue()
        {
            Assert.Equal(ULongEnum.Max, Cbor.Deserialize<ULongEnum>(Bytes("1BFFFFFFFFFFFFFFFF")));   // 2^64 - 1
            Assert.Equal(ULongEnum.High, Cbor.Deserialize<ULongEnum>(Bytes("1B8000000000000000")));   // 2^63
        }

        [Theory]
        [InlineData(LongEnum.Big, "1B000000012A05F200")]                      // 5000000000; used to write 1A2A05F200
        [InlineData(LongEnum.Min, "3B7FFFFFFFFFFFFFFF")]                      // -2^63
        [InlineData(LongEnum.Max, "1B7FFFFFFFFFFFFFFF")]                      // 2^63 - 1
        public void ALongEnumWritesAllSixtyFourBits(LongEnum value, string expected)
        {
            Helper.TestWrite(value, expected);
        }

        [Fact]
        public void AUlongEnumWritesAboveLongMaxValue()
        {
            Helper.TestWrite(ULongEnum.Max, "1BFFFFFFFFFFFFFFFF");   // 2^64 - 1; used to write 20
            Helper.TestWrite(ULongEnum.High, "1B8000000000000000");   // 2^63
            Helper.TestWrite(ULongEnum.Big, "1B000000012A05F200");
        }

        [Fact]
        public void AUintEnumWritesAboveIntMaxValue()
        {
            Helper.TestWrite(UIntEnum.Max, "1AFFFFFFFF");              // 4294967295; used to write 20
            Helper.TestWrite((UIntEnum)4_000_000_000u, "1AEE6B2800");  // used to write 3A1194D7FF
        }

        [Fact]
        public void AnUnnamedValueIsWrittenAsItsNumberAtFullWidthUnderWriteToString()
        {
            Helper.TestWrite((LongEnum)5_000_000_001L, "1B000000012A05F201", null, ToStringOptions);
            Helper.TestWrite((ULongEnum)ulong.MaxValue - 1, "1BFFFFFFFFFFFFFFFE", null, ToStringOptions);
            Helper.TestWrite((UIntEnum)4_000_000_001u, "1AEE6B2801", null, ToStringOptions);
        }

        [Fact]
        public void ADictionaryKeyedByALongEnumKeepsItsKeys()
        {
            Dictionary<LongEnum, int> dictionary = new Dictionary<LongEnum, int>
            {
                [LongEnum.Big] = 1,
                [LongEnum.Min] = 2,
            };

            // {5000000000: 1, -9223372036854775808: 2}
            Helper.TestWrite(dictionary, "A21B000000012A05F20001" + "3B7FFFFFFFFFFFFFFF02");

            Dictionary<LongEnum, int> read = Cbor.Deserialize<Dictionary<LongEnum, int>>(
                Bytes("A21B000000012A05F20001" + "3B7FFFFFFFFFFFFFFF02"));

            Assert.Equal(dictionary, read);
        }

        // ------------------------------------------------------------------------------------------
        // A number that is in range but names no member

        /// <summary>
        /// Accepted, as System.Text.Json accepts it: a [Flags] combination has no member of its own,
        /// and a peer built against a newer enum may send one this build has not heard of.
        /// </summary>
        [Fact]
        public void AnInRangeNumberThatNamesNoMemberIsAccepted()
        {
            Assert.Equal((Colours)7, Cbor.Deserialize<Colours>(Bytes("07")));          // Red | Green | Blue
            Assert.Equal((Colours)0xF0, Cbor.Deserialize<Colours>(Bytes("18F0")));     // no member, no combination
            Assert.Equal((LongEnum)42, Cbor.Deserialize<LongEnum>(Bytes("182A")));
        }

        /// <summary>
        /// A combination is written as its number under WriteToString too, at the width of the type.
        /// </summary>
        [Fact]
        public void AFlagsCombinationIsWrittenAsItsNumberAtTheWidthOfTheType()
        {
            Helper.TestWrite(Colours.Red | Colours.Green, "03", null, ToStringOptions);
            Helper.TestWrite(Colours.Red, "63526564", null, ToStringOptions);   // "Red"
            Helper.TestWrite(Wide.Low | Wide.High, "1B8000000000000001", null, ToStringOptions);
            Helper.TestWrite(Wide.Low | Wide.High, "1B8000000000000001");

            Assert.Equal(Wide.Low | Wide.High, Cbor.Deserialize<Wide>(Bytes("1B8000000000000001")));
            Assert.Equal(Colours.Red | Colours.Green, Cbor.Deserialize<Colours>(Bytes("03")));
        }

        // ------------------------------------------------------------------------------------------
        // Names

        [Fact]
        public void ANonAsciiNameIsWrittenAsUtf8()
        {
            Helper.TestWrite(Accented.Größe, "674772C3B6C39F65", null, ToStringOptions);                  // "Größe"
            Helper.TestWrite(Folded.É, "62C389", null, ToStringOptions);                                  // "É"
            Helper.TestWrite(Folded.È, "62C388", null, ToStringOptions);                                  // "È"
            Helper.TestWrite(Accented.GrößenÄnderung, "714772C3B6C39F656EC384" + "6E646572756E67", null, ToStringOptions);
        }

        [Fact]
        public void ANonAsciiNameReadsAsTheMemberItNames()
        {
            Assert.Equal(Accented.Größe, Cbor.Deserialize<Accented>(Bytes("674772C3B6C39F65")));
            Assert.Equal(Accented.Straße, Cbor.Deserialize<Accented>(Bytes("6753747261C39F65")));
            Assert.Equal(Accented.GrößenÄnderung, Cbor.Deserialize<Accented>(Bytes("714772C3B6C39F656EC384" + "6E646572756E67")));
            Assert.Equal(Folded.É, Cbor.Deserialize<Folded>(Bytes("62C389")));
            Assert.Equal(Folded.È, Cbor.Deserialize<Folded>(Bytes("62C388")));
        }

        [Theory]
        [InlineData(Accented.Größe)]
        [InlineData(Accented.Straße)]
        [InlineData(Accented.GrößenÄnderung)]
        public void ANonAsciiNameRoundTrips(Accented value)
        {
            Assert.Equal(value, Cbor.Deserialize<Accented>(Bytes(Write(typeof(Accented), value, ToStringOptions))));
        }

        /// <summary>
        /// Two names that differ only by non-ASCII letters are two names. The converter is built the
        /// first time the type is used, whatever the format, so building it must not throw.
        /// </summary>
        [Fact]
        public void TwoNamesThatDifferOnlyByNonAsciiLettersAreDistinct()
        {
            Helper.TestWrite(Folded.É, "01");
            Helper.TestWrite(Folded.È, "02");
            Assert.Equal(Folded.È, Cbor.Deserialize<Folded>(Bytes("02")));
            Assert.NotEqual(
                Cbor.Deserialize<Folded>(Bytes("62C389")),
                Cbor.Deserialize<Folded>(Bytes("62C388")));
        }

        [Fact]
        public void AnAsciiFoldedNameIsNotAMember()
        {
            // What an ASCII encoding made of "Größe": two replacement characters in place of the two
            // that are not ASCII. It names nothing.
            Assert.NotNull(Refusal<Accented>(Bytes("65" + "47723F3F65")));
        }

        [Fact]
        public void AnUnknownNameIsACborExceptionThatQuotesItAsText()
        {
            CborException exception = Refusal<Accented>(Bytes("64C389C389"));   // "ÉÉ"

            Assert.NotNull(exception);
            Assert.Contains("ÉÉ", exception.Message);
        }

        // ------------------------------------------------------------------------------------------
        // Anything that is neither a number nor a name

        public static readonly object[][] NotANumberOrAName =
        {
            new object[] { "F6" },                   // null
            new object[] { "F7" },                   // undefined
            new object[] { "F5" },                   // true
            new object[] { "F4" },                   // false
            new object[] { "F93C00" },               // half-float 1.0
            new object[] { "FA3FC00000" },           // single 1.5
            new object[] { "FB3FF8000000000000" },   // double 1.5
            new object[] { "80" },                   // []
            new object[] { "820102" },               // [1, 2]
            new object[] { "9FFF" },                 // [_ ]
            new object[] { "A0" },                   // {}
            new object[] { "A1616101" },             // {"a": 1}
            new object[] { "4101" },                 // h'01'
            new object[] { "40" },                   // h''
        };

        [Theory]
        [MemberData(nameof(NotANumberOrAName))]
        public void ANonNumberNonNameAtTheRootIsACborException(string hex)
        {
            Assert.NotNull(Refusal<IntEnum>(Bytes(hex)));
            Assert.NotNull(Refusal<LongEnum>(Bytes(hex)));
            Assert.NotNull(Refusal<Colours>(Bytes(hex), ToStringOptions));
        }

        [Theory]
        [MemberData(nameof(NotANumberOrAName))]
        public void ANonNumberNonNameAsAMemberIsACborExceptionWithItsPath(string hex)
        {
            byte[] value = Bytes(hex);

            CborException exception = Refusal<Holder>(new byte[] { 0xA1, 0x62, (byte)'E', (byte)'n' }.Concat(value).ToArray());

            Assert.NotNull(exception);
            Assert.Equal("$.En", exception.Path);
        }

        /// <summary>
        /// An enum member refuses what an int member refuses, in the same place, in every object
        /// format -- the control that says the refusal is not special to enums.
        /// </summary>
        [Theory]
        [MemberData(nameof(NotANumberOrAName))]
        public void AnEnumMemberRefusesWhatAnIntMemberRefusesInEveryObjectFormat(string hex)
        {
            byte[] value = Bytes(hex);
            byte[] named = new byte[] { 0xA1, 0x62, (byte)'E', (byte)'n' }.Concat(value).ToArray();
            byte[] keyed = new byte[] { 0xA1, 0x01 }.Concat(value).ToArray();
            byte[] positional = new byte[] { 0x81 }.Concat(value).ToArray();

            AssertSamePath<Holder, IntHolder>(named);
            AssertSamePath<KeyedHolder, KeyedIntHolder>(keyed);
            AssertSamePath<PositionalHolder, PositionalIntHolder>(positional);
        }

        private static void AssertSamePath<TEnumHolder, TIntHolder>(byte[] buffer)
        {
            CborException control = Refusal<TIntHolder>(buffer);
            CborException exception = Refusal<TEnumHolder>(buffer);

            Assert.NotNull(control);
            Assert.NotNull(exception);
            Assert.Equal(control.Path, exception.Path);
        }

        [Fact]
        public void AnEnumGivenNullIsRefusedAsAnIntIs()
        {
            // {"En": null}: the case the audit started from
            CborException exception = Refusal<Holder>(Bytes("A162456EF6"));

            Assert.NotNull(exception);
            Assert.Equal("$.En", exception.Path);
        }

        [Fact]
        public void ACatchOfCborExceptionCatchesAnEnumGivenNull()
        {
            bool caught = false;

            try
            {
                Cbor.Deserialize<Holder>(Bytes("A162456EF6"));
            }
            catch (CborException)
            {
                caught = true;
            }

            Assert.True(caught);
        }

        // ------------------------------------------------------------------------------------------
        // A nullable enum

        [Fact]
        public void ANullableEnumReadsNullAsNull()
        {
            Assert.Null(Cbor.Deserialize<NullableHolder>(Bytes("A162456EF6")).En);   // {"En": null}
            Assert.Null(Cbor.Deserialize<IntEnum?>(Bytes("F6")));
        }

        [Fact]
        public void ANullableEnumReadsANumberAndAName()
        {
            Assert.Equal(IntEnum.One, Cbor.Deserialize<NullableHolder>(Bytes("A162456E01")).En);
            Assert.Equal(IntEnum.Max, Cbor.Deserialize<NullableHolder>(Bytes("A162456E1A7FFFFFFF")).En);
            Assert.Equal(LongEnum.Big, Cbor.Deserialize<LongEnum?>(Bytes("1B000000012A05F200")));
            Assert.Equal(Folded.È, Cbor.Deserialize<Folded?>(Bytes("62C388")));
        }

        [Theory]
        [InlineData("F5")]       // true
        [InlineData("80")]       // []
        [InlineData("A0")]       // {}
        [InlineData("F93C00")]   // 1.0
        [InlineData("4101")]     // h'01'
        public void ANullableEnumGivenSomethingElseIsACborExceptionWithItsPath(string hex)
        {
            CborException exception = Refusal<NullableHolder>(new byte[] { 0xA1, 0x62, (byte)'E', (byte)'n' }.Concat(Bytes(hex)).ToArray());

            Assert.NotNull(exception);
            Assert.Equal("$.En", exception.Path);
        }

        [Fact]
        public void ANullableEnumOutOfRangeIsACborException()
        {
            Assert.NotNull(Refusal<IntEnum?>(Bytes("1B0000000100000001")));
            Assert.NotNull(Refusal<ByteEnum?>(Bytes("19012C")));
        }

        [Fact]
        public void ANullableEnumWritesNullAndTheFullWidthNumber()
        {
            Helper.TestWrite((LongEnum?)null, "F6");
            Helper.TestWrite((LongEnum?)LongEnum.Big, "1B000000012A05F200");
            Helper.TestWrite((ULongEnum?)ULongEnum.Max, "1BFFFFFFFFFFFFFFFF");
        }

        // ------------------------------------------------------------------------------------------
        // The generated path registers the same converter, so it keeps the same width and the same names

        [Fact]
        public void AGeneratedContextWritesTheNumberAtTheFullWidthOfTheType()
        {
            EnumWidthContext context = CborSerializerContext.Default<EnumWidthContext>();
            EnumWidthHolder value = WidthSample();

            string hex = Helper.Write(value, context.Options);

            Assert.Equal(
                "A5"
                + "664E6172726F77" + "387F"                  // "Narrow": -128
                + "644C6F6E67" + "1B000000012A05F201"        // "Long": 5000000001
                + "65554C6F6E67" + "1BFFFFFFFFFFFFFFFF"      // "ULong": 2^64 - 1
                + "6455496E74" + "1AEE6B2801"                // "UInt": 4000000001
                + "644E616D65" + "01",                       // "Name": 1
                hex);

            EnumWidthHolder read = Cbor.Deserialize<EnumWidthHolder>(Bytes(hex), context.Options);

            Assert.Equal(value.Narrow, read.Narrow);
            Assert.Equal(value.Long, read.Long);
            Assert.Equal(value.ULong, read.ULong);
            Assert.Equal(value.UInt, read.UInt);
            Assert.Equal(value.Name, read.Name);
        }

        [Fact]
        public void AGeneratedContextWritesNamesAsUtf8AndAnUnnamedValueAsItsNumber()
        {
            EnumWidthNamesContext context = CborSerializerContext.Default<EnumWidthNamesContext>();
            EnumWidthHolder value = WidthSample();

            string hex = Helper.Write(value, context.Options);

            Assert.Equal(
                "A5"
                + "664E6172726F77" + "63" + "4D696E"        // "Narrow": "Min"
                + "644C6F6E67" + "1B000000012A05F201"       // "Long": 5000000001, which has no name
                + "65554C6F6E67" + "63" + "4D6178"          // "ULong": "Max"
                + "6455496E74" + "1AEE6B2801"               // "UInt": 4000000001, which has no name
                + "644E616D65" + "674772C3B6C39F65",        // "Name": "Größe"
                hex);

            EnumWidthHolder read = Cbor.Deserialize<EnumWidthHolder>(Bytes(hex), context.Options);

            Assert.Equal(value.Narrow, read.Narrow);
            Assert.Equal(value.Long, read.Long);
            Assert.Equal(value.ULong, read.ULong);
            Assert.Equal(value.UInt, read.UInt);
            Assert.Equal(value.Name, read.Name);
        }

        internal static EnumWidthHolder WidthSample()
        {
            return new EnumWidthHolder
            {
                Narrow = SByteEnum.Min,
                Long = (LongEnum)5_000_000_001L,
                ULong = ULongEnum.Max,
                UInt = (UIntEnum)4_000_000_001u,
                Name = Accented.Größe,
            };
        }
    }

    public class EnumWidthHolder
    {
        public EnumConverterTests.SByteEnum Narrow { get; set; }
        public EnumConverterTests.LongEnum Long { get; set; }
        public EnumConverterTests.ULongEnum ULong { get; set; }
        public EnumConverterTests.UIntEnum UInt { get; set; }
        public EnumConverterTests.Accented Name { get; set; }
    }

    [CborSerializable(typeof(EnumWidthHolder))]
    public partial class EnumWidthContext : CborSerializerContext
    {
    }

    [CborSerializable(typeof(EnumWidthHolder))]
    [CborSourceGenerationOptions(EnumFormat = ValueFormat.WriteToString)]
    public partial class EnumWidthNamesContext : CborSerializerContext
    {
    }
}
