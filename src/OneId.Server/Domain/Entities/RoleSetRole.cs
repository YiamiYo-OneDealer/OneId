namespace OneId.Server.Domain.Entities;

public class RoleSetRole
{
    public Guid RoleSetId { get; set; }
    public Guid RoleId { get; set; }
    public RoleSet RoleSet { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
