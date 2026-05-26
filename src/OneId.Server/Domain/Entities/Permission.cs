using OneId.Server.Domain.Enums;

namespace OneId.Server.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public required string PermissionId { get; set; }
    public required string Label { get; set; }
    public PermissionStatus Status { get; set; } = PermissionStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
