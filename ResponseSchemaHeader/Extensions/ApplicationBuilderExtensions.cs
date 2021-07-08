using ResponseSchemaHeader;
using System;

namespace Microsoft.AspNetCore.Builder
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseResponseSchemaHeader(this IApplicationBuilder app, Action<ResponseSchemaHeaderOptions>? setupAction = null)
        {
            ResponseSchemaHeaderOptions options = new();
            setupAction?.Invoke(options);

            return app.UseMiddleware<RequestCultureMiddleware>(options);
        }
    }
}
