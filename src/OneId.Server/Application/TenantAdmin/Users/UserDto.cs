namespace OneId.Server.Application.TenantAdmin.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    Guid TenantId,
    bool IsActive,
    bool IsTenantAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
