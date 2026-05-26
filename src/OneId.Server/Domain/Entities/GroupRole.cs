// Stub: GroupId has no FK to Group entity — Group entity added in Story 4a.4.
// Exists now so DeleteRoleHandler can check role-in-use before Story 4a.4 adds groups.
namespace OneId.Server.Domain.Entities;

public class GroupRole
{
    public Guid GroupId { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
