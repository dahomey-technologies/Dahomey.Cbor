using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Dahomey.Cbor.AspNetCore
{
    public static class CborMvcCoreBuilderExtensions
    {
        public static IMvcBuilder AddDahomeyCbor(this IMvcBuilder builder, CborOptions cborOptions = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.Configure<MvcOptions>(config =>
            {
                config.InputFormatters.Add(new CborInputFormatter(cborOptions));
                config.OutputFormatters.Add(new CborOutputFormatter(cborOptions));
            });

            return builder;
        }
    }
}
