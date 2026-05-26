namespace OneId.Server.Domain.Entities;

public class UserDimensionAssignment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DimensionValueId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public User User { get; set; } = null!;
    public DimensionValue DimensionValue { get; set; } = null!;
}
