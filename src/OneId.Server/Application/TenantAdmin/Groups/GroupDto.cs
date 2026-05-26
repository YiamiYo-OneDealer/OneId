namespace OneId.Server.Application.TenantAdmin.Groups;

public sealed record RoleSummaryDto(Guid Id, string Name);
public sealed record RoleSetSummaryDto(Guid Id, string Name);

public sealed record GroupDto(
    Guid Id,
    string Name,
    IReadOnlyList<RoleSummaryDto> Roles,
    IReadOnlyList<RoleSetSummaryDto> RoleSets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
