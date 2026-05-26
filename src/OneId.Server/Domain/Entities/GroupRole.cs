namespace OneId.Server.Domain.Entities;

public class GroupRole
{
    public Guid GroupId { get; set; }
    public Guid RoleId { get; set; }
    public Group Group { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
