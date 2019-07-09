using Dahomey.Cbor.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Dahomey.Cbor.Tests
{
    [TestClass]
    public class CborOuputFormatterTests
    {
        [TestMethod]
        public async Task WriteSimpleObject()
        {
            SimpleObject obj = new SimpleObject
            {
                Boolean = true,
                Byte = 12,
                SByte = 13,
                Int16 = 14,
                UInt16 = 15,
                Int32 = 16,
                UInt32 = 17u,
                Int64 = 18,
                UInt64 = 19ul,
                Single = 20.21f,
                Double = 22.23,
                String = "string",
                DateTime = new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc),
                Enum = EnumTest.Value1
            };

            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            byte[] buffer = hexBuffer.HexToBytes();
            MemoryStream body = new MemoryStream(); ;

            Mock<HttpResponse> httpResponse = new Mock<HttpResponse>(MockBehavior.Strict);
            httpResponse.SetupSet(r => r.ContentType = "application/cbor");
            httpResponse.Setup(r => r.Body).Returns(body);

            Mock<HttpContext> httpContext = new Mock<HttpContext>(MockBehavior.Strict);
            httpContext.Setup(c => c.Response).Returns(httpResponse.Object);

            Mock<ModelMetadata> modelMetadata = new Mock<ModelMetadata>(
                MockBehavior.Strict,
                ModelMetadataIdentity.ForType(typeof(SimpleObject)));

            OutputFormatterWriteContext context = new OutputFormatterWriteContext(
                httpContext.Object,
                (stream, encoding) => new StreamWriter(stream, encoding),
                typeof(SimpleObject),
                obj);

            IOutputFormatter outputFormatter = new CborOutputFormatter(
                new CborOptions
                {
                    EnumFormat = ValueFormat.WriteToString
                });

            Assert.IsTrue(outputFormatter.CanWriteResult(context));

            await outputFormatter.WriteAsync(context);

            string actualHexBuffer = BitConverter.ToString(body.ToArray()).Replace("-", "");
            Assert.AreEqual(hexBuffer, actualHexBuffer);
        }
    }
}
