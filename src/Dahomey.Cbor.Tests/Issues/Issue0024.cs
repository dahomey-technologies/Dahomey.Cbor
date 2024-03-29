﻿using Dahomey.Cbor.ObjectModel;
using Dahomey.Cbor.Tests.Extensions;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0024
    {
        [Fact]
        public void Test()
        {
            const string hexBuffer = "BF65627974657341016C64657374696E6174696F6E738151027BE4CA91F62F11794CCF66A92B7714066466726F6D58270402023B2DC2EAA3A237C9AB13E3EC0357679DB51EFB63A7DC95980AD81A90D9B0EBD30C695A1B686D65746144617461BF6B6170706C69636174696F6E6474657374FF656E6F6E63651B1E4498A9ECD5A1876A73657269616C697A65727772616469782E7061727469636C65732E6D65737361676562746F58270402023B2DC2EAA3A237C9AB13E3EC0357679DB51EFB63A7DC95980AD81A90D9B0EBD30C695A1B6776657273696F6E1864FF";
            byte[] data = hexBuffer.HexToBytes();
            CborObject obj = Cbor.Deserialize<CborObject>(data);

            Assert.NotNull(obj);
            Assert.Equal(8, obj.Count);
        }
    }
}
