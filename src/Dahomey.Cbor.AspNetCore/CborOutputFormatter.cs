using Dahomey.Cbor.Serialization;
using Dahomey.Cbor.Serialization.Converters;
using Dahomey.Cbor.Util;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System.Threading.Tasks;

namespace Dahomey.Cbor.AspNetCore
{
    public class CborOutputFormatter : OutputFormatter
    {
        private readonly CborOptions _cborOptions;

        public CborOutputFormatter(CborOptions cborOptions)
        {
            _cborOptions = cborOptions;

            SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/cbor"));
        }

        public override Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
        {
            using (ByteBufferWriter bufferWriter = new ByteBufferWriter())
            {
                CborWriter writer = new CborWriter(bufferWriter, _cborOptions);
                ICborConverter cborConverter = CborConverter.Lookup(context.Object.GetType());
                cborConverter.Write(ref writer, context.Object);
                return bufferWriter.CopyToAsync(context.HttpContext.Response.Body);
            }
        }
    }
}
