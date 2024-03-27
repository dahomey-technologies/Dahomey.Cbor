using System;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests
{
    public class AnonymousTests
    {
        [Fact]
        public void ReadAnonymous()
        {
            CborOptions options = new CborOptions();

            const string hexBuffer = @"A26249640C644E616D6563666F6F";
            var prototype = new { Id = default(int), Name = default(string) };

            Span<byte> buffer = hexBuffer.HexToBytes();
            var obj = Cbor.DeserializeAnonymousType(buffer, prototype, options);

            Assert.Equal(12, obj.Id);
            Assert.Equal("foo", obj.Name);
        }

        [Fact]
        public void WriteAnonymous()
        {
            CborOptions options = new CborOptions();

            const string hexBuffer = @"A26249640C644E616D6563666F6F";
            var obj = new { Id = 12, Name = "foo" };

            Helper.TestWrite(obj, hexBuffer, null, options);
        }
    }
}
