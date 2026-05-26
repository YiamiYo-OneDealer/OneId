namespace OneId.Server.Application.Internal;

public sealed record UserDto(
    Guid Id,
    string Email,
    bool IsTenantAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
