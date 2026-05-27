using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Audit;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.TenantAdmin.RoleSets.Commands;

public sealed class DeleteRoleSetHandler(AppDbContext db, ITenantContext tenantContext, IAuditService audit)
{
    public async Task<bool> HandleAsync(Guid id, uint version, CancellationToken ct = default)
    {
        var roleSet = await db.RoleSets
            .Include(rs => rs.GroupRoleSets)
                .ThenInclude(grs => grs.Group)
            .FirstOrDefaultAsync(rs => rs.Id == id, ct);

        if (roleSet is null) return false;

        if (roleSet.GroupRoleSets.Count > 0)
        {
            var groupNames = roleSet.GroupRoleSets.Select(grs => grs.Group.Name).ToList();
            throw new RoleSetInUseException(groupNames);
        }

        db.Entry(roleSet).Property<uint>("xmin").OriginalValue = version;
        db.RoleSets.Remove(roleSet);
        await db.SaveChangesAsync(ct);  // throws DbUpdateConcurrencyException if xmin mismatch

        await audit.AppendAsync(new AuditLogEntry(
            tenantContext.TenantId,
            "role_set.deleted",
            "RoleSet",
            id), ct);
        return true;
    }
}

public sealed class RoleSetInUseException(IReadOnlyList<string> groupNames)
    : Exception($"RoleSet is assigned to groups: {string.Join(", ", groupNames)}")
{
    public IReadOnlyList<string> GroupNames { get; } = groupNames;
}
