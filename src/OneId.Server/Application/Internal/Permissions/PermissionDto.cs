namespace OneId.Server.Application.Internal.Permissions;

public sealed record PermissionDto(
    Guid Id,
    string PermissionId,
    string Label,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
