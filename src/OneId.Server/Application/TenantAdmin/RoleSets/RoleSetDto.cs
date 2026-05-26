namespace OneId.Server.Application.TenantAdmin.RoleSets;

public sealed record RoleSetDto(
    Guid Id,
    string Name,
    IReadOnlyList<RoleSummaryDto> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
