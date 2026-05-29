using Microsoft.Extensions.DependencyInjection;
using OneId.Server.Application.Permissions;
using OneId.Server.Application.TenantAdmin.Dimensions.Commands;
using OneId.Server.Application.TenantAdmin.Dimensions.Queries;
using OneId.Server.Application.TenantAdmin.Groups.Commands;
using OneId.Server.Application.TenantAdmin.Groups.Queries;
using OneId.Server.Application.TenantAdmin.Roles.Commands;
using OneId.Server.Application.TenantAdmin.Roles.Queries;
using OneId.Server.Application.TenantAdmin.RoleSets.Commands;
using OneId.Server.Application.TenantAdmin.RoleSets.Queries;
using OneId.Server.Application.TenantAdmin.UserOverrides.Commands;
using OneId.Server.Application.TenantAdmin.UserOverrides.Queries;
using OneId.Server.Application.TenantAdmin.Users.Commands;
using OneId.Server.Application.TenantAdmin.Users.Queries;

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
        services.AddScoped<ListGroupsHandler>();
        services.AddScoped<GetGroupHandler>();
        services.AddScoped<GetUserGroupsHandler>();
        services.AddScoped<CreateGroupHandler>();
        services.AddScoped<UpdateGroupHandler>();
        services.AddScoped<DeleteGroupHandler>();
        services.AddScoped<AddGroupMemberHandler>();
        services.AddScoped<RemoveGroupMemberHandler>();
        services.AddScoped<ListDimensionValuesHandler>();
        services.AddScoped<AddDimensionValueHandler>();
        services.AddScoped<DeactivateDimensionValueHandler>();
        services.AddScoped<GetUserDimensionsHandler>();
        services.AddScoped<SetUserDimensionsHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<GetUserHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<DeleteUserHandler>();
        services.AddScoped<ListUserOverridesHandler>();
        services.AddScoped<CreateUserOverrideHandler>();
        services.AddScoped<DeleteUserOverrideHandler>();
        services.AddScoped<GetEffectivePermissionsHandler>();
        services.AddScoped<EffectivePermissionsPreviewHandler>();
        return services;
    }
}
