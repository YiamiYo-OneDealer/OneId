using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.TenantAdmin.Roles.Commands;
using OneId.Server.Application.TenantAdmin.Roles.Queries;

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
        return services;
    }
}
