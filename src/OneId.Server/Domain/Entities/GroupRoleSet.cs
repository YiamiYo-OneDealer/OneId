// STUB: GroupId has no FK to Group entity — Group entity added in Story 4a.4.
// Exists now so DeleteRoleSetHandler can check role-set-in-use before Story 4a.4 adds groups.
namespace OneId.Server.Domain.Entities;

public class GroupRoleSet
{
    public Guid GroupId { get; set; }
    public Guid RoleSetId { get; set; }
    public RoleSet RoleSet { get; set; } = null!;
}
