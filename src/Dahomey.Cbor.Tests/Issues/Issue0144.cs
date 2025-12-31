using Dahomey.Cbor.Serialization;
using Xunit;
using System;
using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Tests.Extensions;

namespace Dahomey.Cbor.Tests.Issues;

public class Issue0144
{
    [Theory]
    [InlineData("1A5D2A3DDB", null)]
    [InlineData("C01A5D2A3DDB", 0ul)]
    [InlineData("D8641A5D2A3DDB", 100ul)]
    public void ReadSemanticTag(string hexBuffer, ulong? expectedTag)
    {
        byte[] buffer = hexBuffer.HexToBytes();
        
        CborReader reader = new CborReader(buffer.AsSpan());
        CborValue value = new CborValueConverter(new CborOptions()).Read(ref reader);
        
        Assert.Equal(value.SemanticTag, expectedTag);
    }
}