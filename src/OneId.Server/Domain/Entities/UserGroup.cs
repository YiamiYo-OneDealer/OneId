namespace OneId.Server.Domain.Entities;

public class UserGroup
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Group Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
