using OneId.Server.Application.Common;

namespace OneId.Server.Infrastructure.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var tidClaim = context.User.FindFirst("tid")?.Value;
        if (tidClaim is not null && Guid.TryParse(tidClaim, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.Initialize(tenantId);
        }
        await next(context);
    }
}
