namespace OneId.Server.Domain.Entities;

public class RoleSet
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<RoleSetRole> RoleSetRoles { get; set; } = [];
    public ICollection<GroupRoleSet> GroupRoleSets { get; set; } = [];
}
