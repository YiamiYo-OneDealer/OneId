using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.TenantAdmin.Roles.Commands;
using OneId.Server.Application.TenantAdmin.Roles.Queries;
using OneId.Server.Application.TenantAdmin.RoleSets.Commands;
using OneId.Server.Application.TenantAdmin.RoleSets.Queries;

namespace OneId.Server.Application.TenantAdmin;

public static class TenantServiceExtensions
{
    public static IServiceCollection AddTenantAdminHandlers(this IServiceCollection services)
    {
        services.AddScoped<ListRolesHandler>();
        services.AddScoped<GetRoleHandler>();
        services.AddScoped<CreateRoleHandler>();
        services.AddScoped<UpdateRoleHandler>();
        services.AddScoped<DeleteRoleHandler>();
        services.AddScoped<ListRoleSetsHandler>();
        services.AddScoped<GetRoleSetHandler>();
        services.AddScoped<CreateRoleSetHandler>();
        services.AddScoped<UpdateRoleSetHandler>();
        services.AddScoped<DeleteRoleSetHandler>();
        return services;
    }
}
