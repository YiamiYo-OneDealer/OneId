namespace OneId.Server.Application.TenantAdmin.UserOverrides;

public sealed record UserOverrideDto(
    Guid Id,
    string PermissionId,
    string OverrideType,
    string Reason,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    bool IsExpired);
