namespace OneId.Server.Domain.Entities;

public class Role
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<GroupRole> GroupRoles { get; set; } = [];
}
