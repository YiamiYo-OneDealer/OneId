namespace OneId.Server.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<GroupRole> GroupRoles { get; set; } = [];
    public ICollection<GroupRoleSet> GroupRoleSets { get; set; } = [];
    public ICollection<UserGroup> UserGroups { get; set; } = [];
}
