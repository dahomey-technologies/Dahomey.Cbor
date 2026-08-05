using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.ObjectModel;
using System;
using System.Collections.Generic;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// Every value the deterministic key work passes through, written with default options, pinned to
    /// the bytes the library produced before that work existed.
    /// </summary>
    /// <remarks>
    /// The ordering feature is opt-in, so the load-bearing claim is not only that
    /// <c>Deterministic = true</c> sorts correctly but that <c>Deterministic = false</c> — every
    /// existing caller — writes exactly what it wrote before. The key path encodes each key through
    /// its own converter, which touches map writing for objects, dictionaries and
    /// <see cref="CborObject"/> alike; a per-feature test cannot show that the untouched setting is
    /// untouched, and reading a diff cannot either.
    /// <para>
    /// The expected bytes are not hand-written. They were captured by running <see cref="Corpus"/>
    /// unchanged on the base commit and recording what came out, so a disagreement here means this
    /// branch moved the default output rather than that someone predicted it wrongly. To regenerate
    /// after adding a case, run the corpus on the base commit and print each entry.
    /// </para>
    /// </remarks>
    public class DeterministicByteCompatibilityTests
    {
        private class StringKeyed
        {
            public int Zebra { get; set; }
            public int Apple { get; set; }
            public int Mango { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.IntKeyMap)]
        private class IntKeyed
        {
            [CborProperty(-1)]
            public int Negative { get; set; }
            [CborProperty(0)]
            public int Zero { get; set; }
            [CborProperty(7)]
            public int Seven { get; set; }
        }

        [CborObjectFormat(CborObjectFormat.Array)]
        private class ArrayFormatted
        {
            [CborProperty(0)]
            public int Zebra { get; set; }
            [CborProperty(1)]
            public int Apple { get; set; }
        }

        private class Nesting
        {
            public int Yak { get; set; }
            public StringKeyed Inner { get; set; }
            public int Ant { get; set; }
        }

        private enum Aliased
        {
            First = 1,
            AlsoFirst = 1,
            Second = 2,
        }

        [Flags]
        private enum Flagged
        {
            None = 0,
            A = 1,
            B = 2,
        }

        [CborDiscriminator("base")]
        private class Base
        {
            public int Common { get; set; }
        }

        [CborDiscriminator("derived")]
        private class Derived : Base
        {
            public int Extra { get; set; }
        }

        /// <summary>
        /// Name → a write of that value under default options. Each entry serializes at its own static
        /// type, which is what selects the converter under test.
        /// </summary>
        private static Dictionary<string, Func<string>> Corpus()
        {
            return new Dictionary<string, Func<string>>
            {
                ["object.stringKeyMap"] = () => Helper.Write(new StringKeyed { Zebra = 1, Apple = 2, Mango = 3 }),
                ["object.intKeyMap"] = () => Helper.Write(new IntKeyed { Negative = 1, Zero = 2, Seven = 3 }),
                ["object.arrayFormat"] = () => Helper.Write(new ArrayFormatted { Zebra = 1, Apple = 2 }),
                ["object.nested"] = () => Helper.Write(new Nesting
                {
                    Yak = 1,
                    Inner = new StringKeyed { Zebra = 4, Apple = 5, Mango = 6 },
                    Ant = 2,
                }),
                ["object.polymorphic"] = () => Helper.Write<Base>(new Derived { Common = 1, Extra = 2 }),

                ["dictionary.string"] = () => Helper.Write(new Dictionary<string, int>
                {
                    ["zebra"] = 1,
                    ["apple"] = 2,
                    ["z"] = 3,
                    ["aa"] = 4,
                }),
                ["dictionary.int"] = () => Helper.Write(new Dictionary<int, string>
                {
                    [10] = "a",
                    [-1] = "b",
                    [0] = "c",
                    [65536] = "d",
                }),
                ["dictionary.long"] = () => Helper.Write(new Dictionary<long, string>
                {
                    [4294967301L] = "a",
                    [10L] = "b",
                }),
                ["dictionary.char"] = () => Helper.Write(new Dictionary<char, int>
                {
                    ['z'] = 1,
                    ['a'] = 2,
                }),
                ["dictionary.enum.aliased"] = () => Helper.Write(new Dictionary<Aliased, int>
                {
                    [Aliased.Second] = 1,
                    [Aliased.First] = 2,
                }),
                ["dictionary.enum.flags"] = () => Helper.Write(new Dictionary<Flagged, int>
                {
                    [Flagged.A | Flagged.B] = 1,
                    [Flagged.None] = 2,
                }),
                ["dictionary.double"] = () => Helper.Write(new Dictionary<double, int>
                {
                    [2.5] = 1,
                    [-1.5] = 2,
                }),
                ["dictionary.bool"] = () => Helper.Write(new Dictionary<bool, int>
                {
                    [true] = 1,
                    [false] = 2,
                }),
                ["dictionary.byteArray"] = () => Helper.Write(new Dictionary<byte[], int>
                {
                    [new byte[] { 0x02 }] = 1,
                    [new byte[] { 0x01 }] = 2,
                }),
                ["dictionary.nested"] = () => Helper.Write(new Dictionary<string, Dictionary<string, int>>
                {
                    ["outer"] = new Dictionary<string, int> { ["z"] = 1, ["a"] = 2 },
                }),

                ["cborObject.mixedKeys"] = () => Helper.Write(new CborObject
                {
                    ["zebra"] = 1,
                    [(ulong)10] = 2,
                    [(ulong)4294967301] = 3,
                    ["a"] = 4,
                }),
                ["cborObject.nested"] = () => Helper.Write(new CborObject
                {
                    ["outer"] = new CborObject { ["z"] = 1, ["a"] = 2 },
                }),
            };
        }

        /// <summary>
        /// Name → the bytes the base commit writes for that value under default options.
        /// </summary>
        private static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            ["object.stringKeyMap"] = "A3655A6562726101654170706C6502654D616E676F03",
            ["object.intKeyMap"] = "A3200100020703",
            ["object.arrayFormat"] = "820102",
            ["object.nested"] = "A36359616B0165496E6E6572A3655A6562726104654170706C6505654D616E676F0663416E7402",
            ["object.polymorphic"] = "A3625F7467646572697665646545787472610266436F6D6D6F6E01",
            ["dictionary.string"] = "A4657A6562726101656170706C6502617A0362616104",
            ["dictionary.int"] = "A40A61612061620061631A000100006164",
            ["dictionary.long"] = "A21B000000010000000561610A6162",
            ["dictionary.char"] = "A2617A01616102",
            ["dictionary.enum.aliased"] = "A202010102",
            ["dictionary.enum.flags"] = "A203010002",
            ["dictionary.double"] = "A2F9410001F9BE0002",
            ["dictionary.bool"] = "A2F501F402",
            ["dictionary.byteArray"] = "A2410201410102",
            ["dictionary.nested"] = "A1656F75746572A2617A01616102",
            ["cborObject.mixedKeys"] = "A4657A65627261010A021B000000010000000503616104",
            ["cborObject.nested"] = "A1656F75746572A2617A01616102",
        };

        public static IEnumerable<object[]> CorpusNames()
        {
            foreach (string name in Corpus().Keys)
            {
                yield return new object[] { name };
            }
        }

        [Theory]
        [MemberData(nameof(CorpusNames))]
        public void DefaultOptionsWriteTheSameBytesAsTheBaseCommit(string name)
        {
            Assert.True(Expected.ContainsKey(name), $"No pinned bytes for corpus entry '{name}'.");
            Assert.Equal(Expected[name], Corpus()[name]());
        }

    }
}
