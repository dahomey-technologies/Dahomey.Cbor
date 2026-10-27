using System;
using Dahomey.Cbor.Attributes;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    /// <summary>
    /// An omitted member reads back as its default, so [CborIgnoreIfDefault] may omit only a value that
    /// is indistinguishable from the default, not merely equal to it.
    /// </summary>
    public class IgnoreIfDefaultIdentityTests
    {
        public class Holder
        {
            [CborIgnoreIfDefault] public double D { get; set; }
            [CborIgnoreIfDefault] public float F { get; set; }
            [CborIgnoreIfDefault] public Half H { get; set; }
            [CborIgnoreIfDefault] public decimal M { get; set; }
            [CborIgnoreIfDefault] public DateTime T { get; set; }
            [CborIgnoreIfDefault] public DateTimeOffset O { get; set; }
            [CborIgnoreIfDefault] public double? N { get; set; }
        }

        public struct StructHolder
        {
            [CborIgnoreIfDefault] public double D { get; set; }
        }

        [Fact]
        public void TheDefaultItselfIsStillOmitted()
        {
            Assert.Equal("A0", Helper.Write(new Holder()));
            Assert.Equal("A0", Helper.Write(new StructHolder()));
        }

        [Fact]
        public void AValueEqualToTheDefaultButNotIdenticalIsWritten()
        {
            Holder value = new Holder
            {
                D = -0.0,
                F = -0.0f,
                H = (Half)(-0.0f),
                M = 0.00m,
                T = new DateTime(0, DateTimeKind.Utc),
                O = new DateTimeOffset(1, 1, 1, 5, 0, 0, TimeSpan.FromHours(5)),
                N = -0.0,
            };

            Holder read = Cbor.Deserialize<Holder>(Helper.Write(value).HexToBytes());

            Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(read.D));
            Assert.True(float.IsNegative(read.F));
            Assert.True(Half.IsNegative(read.H));
            Assert.Equal(decimal.GetBits(0.00m), decimal.GetBits(read.M));
            Assert.Equal(DateTimeKind.Utc, read.T.Kind);
            Assert.Equal(TimeSpan.FromHours(5), read.O.Offset);
            Assert.True(double.IsNegative(read.N.Value));
        }

        [Fact]
        public void AStructMemberIsComparedTheSameWay()
        {
            Assert.Equal("A16144F98000", Helper.Write(new StructHolder { D = -0.0 }));
        }
    }
}
