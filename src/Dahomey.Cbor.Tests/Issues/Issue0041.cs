using System;
using System.Collections.Generic;
using Dahomey.Cbor.Attributes;
using Xunit;

namespace Dahomey.Cbor.Tests.Issues
{
    public class Issue0041
    {
        public class WebserviceResponse<T>
        {
            [CborProperty("result")]
            public T Result { get; set; }
        }

        public class Conversation
        {
            public List<Key> keys { get; set; }
        }

        public class Key
        {
            public byte[] key { get; set; }
        }

        [Fact]
        public void TestByteArray()
        {
            var hex = "A166726573756C74A1646B65797381A262696401636B657958802382F10772FC8F725D54196944BC1F54DEDEA6231EC6B8DDCCA8CB3AEBAAFDD6A6F16743A6C9AE05B834746C2E335B7EDC3CD4F3BAB6AB44DEB676A9BCD75BC21EC4756B21EAF15A094BCA3ADE75521E473A1C771F51204BF26D5917E47CF4E08D79E8EDABEBBB99CBDB0FF374FEF28EB87E27FDFC47C0360F19E5E957C77226";

            var ex = Record.Exception(() => {
                Helper.Read<WebserviceResponse<Conversation>>(hex, CborOptions.Default);
            });
            Assert.Null(ex);
        }
    }
}
