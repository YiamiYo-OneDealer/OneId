using OneId.Server.Domain.Enums;

namespace OneId.Server.Domain.Entities;

public class UserPermissionOverride
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public required string PermissionId { get; set; }
    public PermissionOverrideType OverrideType { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
