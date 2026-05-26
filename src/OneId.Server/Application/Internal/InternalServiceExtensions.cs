using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Common;
using OneId.Server.Application.Internal.Commands;
using OneId.Server.Application.Internal.Queries;
using OneId.Server.Application.Internal.Permissions.Commands;
using OneId.Server.Application.Internal.Permissions.Queries;

namespace OneId.Server.Application.Internal;

public static class InternalServiceExtensions
{
    // AR-8: all InternalAdminContext references must live inside Application/Internal/ — ArchUnit enforced.
    public static IServiceCollection AddInternalAdminHandlers(this IServiceCollection services)
    {
        services.AddScoped<InternalAdminContext>();
        services.AddScoped<ListTenantsHandler>();
        services.AddScoped<GetTenantHandler>();
        services.AddScoped<CreateTenantHandler>();
        services.AddScoped<UpdateTenantHandler>();
        services.AddScoped<DeactivateTenantHandler>();
        services.AddScoped<DesignateTenantAdminHandler>();
        services.AddScoped<RemoveTenantAdminHandler>();
        services.AddScoped<SuspendTenantHandler>();
        services.AddScoped<ReinstateTenantHandler>();
        // Story 4a.1: Permission catalog handlers
        services.AddScoped<ListPermissionsHandler>();
        services.AddScoped<GetPermissionHandler>();
        services.AddScoped<CreatePermissionHandler>();
        services.AddScoped<UpdatePermissionHandler>();
        services.AddScoped<DeactivatePermissionHandler>();
        return services;
    }
}
