using Microsoft.AspNetCore.Authentication;
using OneId.Server.Application.Common;
using OpenIddict.Validation.AspNetCore;

namespace OneId.Server.Infrastructure.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // AuthenticationMiddleware only sets context.User from the default scheme.
        // No default scheme is configured — OpenIddict validation runs per-endpoint.
        // Authenticate explicitly here so the tid claim is available before routing.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            var result = await context.AuthenticateAsync(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            if (result.Succeeded && result.Principal is not null)
                context.User = result.Principal;
        }

        var tidClaim = context.User?.FindFirst("tid")?.Value;
        if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.Initialize(tenantId);
        }
        await next(context);
    }
}
