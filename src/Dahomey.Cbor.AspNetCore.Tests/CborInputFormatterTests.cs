using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dahomey.Cbor.AspNetCore.Tests
{
    [TestClass]
    public class CborInputFormatterTests
    {
        [TestMethod]
        public async Task ReadSimpleObject()
        {
            const string hexBuffer = "AE67426F6F6C65616EF56553427974650D64427974650C65496E7431360E6655496E7431360F65496E743332106655496E7433321165496E743634126655496E7436341366537472696E6766737472696E676653696E676C65FA41A1AE1466446F75626C65FB40363AE147AE147B684461746554696D6574323031342D30322D32315431393A30303A30305A64456E756D6656616C756531";
            byte[] buffer = hexBuffer.HexToBytes();

            Mock<HttpRequest> httpRequest = new Mock<HttpRequest>(MockBehavior.Strict);
            httpRequest.Setup(r => r.ContentType).Returns("application/cbor");
            httpRequest.Setup(r => r.ContentLength).Returns(buffer.Length);
            httpRequest.Setup(r => r.Body).Returns(new MemoryStream(buffer));

            Mock<HttpContext> httpContext = new Mock<HttpContext>(MockBehavior.Strict);
            httpContext.Setup(c => c.Request).Returns(httpRequest.Object);

            Mock<ModelMetadata> modelMetadata = new Mock<ModelMetadata>(
                MockBehavior.Strict,
                ModelMetadataIdentity.ForType(typeof(SimpleObject)));

            InputFormatterContext context = new InputFormatterContext(
                httpContext.Object,
                nameof(SimpleObject),
                new ModelStateDictionary(),
                modelMetadata.Object,
                (stream, encoding) => new StreamReader(stream, encoding));

            IInputFormatter inputFormatter = new CborInputFormatter(null);

            Assert.IsTrue(inputFormatter.CanRead(context));

            InputFormatterResult result = await inputFormatter.ReadAsync(context);

            Assert.IsFalse(result.HasError);
            Assert.IsTrue(result.IsModelSet);

            SimpleObject obj = (SimpleObject)result.Model;

            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.Boolean);
            Assert.AreEqual(12, obj.Byte);
            Assert.AreEqual(13, obj.SByte);
            Assert.AreEqual(14, obj.Int16);
            Assert.AreEqual(15, obj.UInt16);
            Assert.AreEqual(16, obj.Int32);
            Assert.AreEqual(17u, obj.UInt32);
            Assert.AreEqual(18, obj.Int64);
            Assert.AreEqual(19ul, obj.UInt64);
            Assert.AreEqual(20.21f, obj.Single);
            Assert.AreEqual(22.23, obj.Double);
            Assert.AreEqual("string", obj.String);
            Assert.AreEqual(new DateTime(2014, 02, 21, 19, 0, 0, DateTimeKind.Utc), obj.DateTime);
            Assert.AreEqual(EnumTest.Value1, obj.Enum);
        }

        [DataTestMethod]
        [DataRow("application/json", "")]
        [DataRow("application/cbor", "application/cbor")]
        public void GetSupportedContentTypes(string actualContentType, string expectedContentType)
        {
            IApiRequestFormatMetadataProvider apiRequestFormatMetadataProvider = new CborInputFormatter(null);
            IReadOnlyList<string> contentTypes
                = apiRequestFormatMetadataProvider.GetSupportedContentTypes(
                    actualContentType, typeof(object));

            if (string.IsNullOrEmpty(expectedContentType))
            {
                Assert.IsNull(contentTypes);
            }
            else
            {
                Assert.AreEqual(1, contentTypes.Count);
                Assert.AreEqual(expectedContentType, contentTypes[0]);
            }
        }
    }
}
