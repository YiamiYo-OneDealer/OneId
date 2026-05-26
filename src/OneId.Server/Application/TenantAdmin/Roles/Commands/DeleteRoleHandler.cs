using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.Roles.Commands;

public sealed class DeleteRoleHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var role = await db.Roles
            .Include(r => r.GroupRoles)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (role is null) return false;

        if (role.GroupRoles.Count > 0)
        {
            // Until Story 4a.4 adds the Group entity, GroupId is returned as string.
            // Story 4a.4 will replace this with actual Group.Name values.
            var groupNames = role.GroupRoles.Select(gr => gr.GroupId.ToString()).ToList();
            throw new RoleInUseException(groupNames);
        }

        db.Roles.Remove(role);
        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role.deleted",
            "Role",
            id), ct);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class RoleInUseException(IReadOnlyList<string> groupNames)
    : Exception($"Role is assigned to groups: {string.Join(", ", groupNames)}")
{
    public IReadOnlyList<string> GroupNames { get; } = groupNames;
}
