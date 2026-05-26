namespace OneId.Server.Application.TenantAdmin.Roles;

public sealed record RoleDto(
    Guid Id,
    string Name,
    IReadOnlyList<string> PermissionIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
