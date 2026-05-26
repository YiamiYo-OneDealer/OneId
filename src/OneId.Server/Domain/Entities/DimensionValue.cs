using OneId.Server.Domain.Enums;

namespace OneId.Server.Domain.Entities;

public class DimensionValue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DimensionAxis Axis { get; set; }
    public required string Value { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
