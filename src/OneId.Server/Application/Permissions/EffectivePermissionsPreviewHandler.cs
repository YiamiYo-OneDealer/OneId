using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Permissions;

public sealed record PreviewOverrideEntry(string PermissionId, string Effect);

public sealed record PreviewRequest(
    List<Guid> GroupIds,
    List<Guid> RoleSets,
    List<PreviewOverrideEntry> Overrides);

public sealed class EffectivePermissionsPreviewHandler(AppDbContext db, ITenantContext tenantContext)
{
    public async Task<EffectivePermissionsResponse> HandleAsync(PreviewRequest request, CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId;

        // Tenant isolation: silently filter supplied groupIds to only those in the calling tenant.
        var validGroupIds = request.GroupIds.Count > 0
            ? await db.Groups
                .Where(g => request.GroupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .OrderBy(g => g.Name)
                .ToListAsync(ct)
            : [];

        var groupIds = validGroupIds.Select(g => g.Id).ToList();

        // Direct paths: Group → Role → Permission (for valid groups)
        var directPaths = groupIds.Count > 0
            ? await (
                from gr in db.GroupRoles
                where groupIds.Contains(gr.GroupId)
                join r in db.Roles on gr.RoleId equals r.Id
                join rp in db.RolePermissions on r.Id equals rp.RoleId
                join p in db.Permissions on rp.PermissionId equals p.Id
                orderby r.Name
                select new { gr.GroupId, RoleId = r.Id, r.Name, PermissionId = p.PermissionId }
            ).ToListAsync(ct)
            : [];

        // RoleSet paths via groups
        var roleSetGroupPaths = groupIds.Count > 0
            ? await (
                from grs in db.GroupRoleSets
                where groupIds.Contains(grs.GroupId)
                join rs in db.RoleSets on grs.RoleSetId equals rs.Id
                join rsr in db.RoleSetRoles on rs.Id equals rsr.RoleSetId
                join r in db.Roles on rsr.RoleId equals r.Id
                join rp in db.RolePermissions on r.Id equals rp.RoleId
                join p in db.Permissions on rp.PermissionId equals p.Id
                orderby rs.Name, r.Name
                select new { grs.GroupId, RoleSetId = rs.Id, RoleSetName = rs.Name, RoleId = r.Id, RoleName = r.Name, PermissionId = p.PermissionId }
            ).ToListAsync(ct)
            : [];

        // Direct RoleSet paths (from request.RoleSets — not via a group)
        // Tenant isolation applied via db.RoleSets query filter.
        var validRoleSetIds = request.RoleSets.Count > 0
            ? await db.RoleSets
                .Where(rs => request.RoleSets.Contains(rs.Id))
                .Select(rs => new { rs.Id, rs.Name })
                .ToListAsync(ct)
            : [];

        var directRoleSetPaths = validRoleSetIds.Count > 0
            ? await (
                from rsr in db.RoleSetRoles
                where validRoleSetIds.Select(rs => rs.Id).Contains(rsr.RoleSetId)
                join r in db.Roles on rsr.RoleId equals r.Id
                join rp in db.RolePermissions on r.Id equals rp.RoleId
                join p in db.Permissions on rp.PermissionId equals p.Id
                orderby r.Name
                select new { rsr.RoleSetId, RoleId = r.Id, RoleName = r.Name, PermissionId = p.PermissionId }
            ).ToListAsync(ct)
            : [];

        var permissionMap = new Dictionary<string, PermissionEntryDto>();

        // Build from group direct-role paths
        foreach (var group in validGroupIds)
        {
            foreach (var path in directPaths.Where(p => p.GroupId == group.Id).OrderBy(p => p.Name))
            {
                if (!permissionMap.ContainsKey(path.PermissionId))
                {
                    permissionMap[path.PermissionId] = new PermissionEntryDto(
                        path.PermissionId, "", false,
                        [
                            new ProvenanceNodeDto("group", group.Id.ToString(), "", ""),
                            new ProvenanceNodeDto("role", path.RoleId.ToString(), "", ""),
                            new ProvenanceNodeDto("permission", path.PermissionId, "", ""),
                        ]);
                }
            }

            foreach (var path in roleSetGroupPaths.Where(p => p.GroupId == group.Id)
                                                   .OrderBy(p => p.RoleSetName).ThenBy(p => p.RoleName))
            {
                if (!permissionMap.ContainsKey(path.PermissionId))
                {
                    permissionMap[path.PermissionId] = new PermissionEntryDto(
                        path.PermissionId, "", false,
                        [
                            new ProvenanceNodeDto("group", group.Id.ToString(), "", ""),
                            new ProvenanceNodeDto("roleSet", path.RoleSetId.ToString(), "", ""),
                            new ProvenanceNodeDto("role", path.RoleId.ToString(), "", ""),
                            new ProvenanceNodeDto("permission", path.PermissionId, "", ""),
                        ]);
                }
            }
        }

        // Build from direct roleSet paths (not via a group)
        foreach (var rsEntry in validRoleSetIds)
        {
            foreach (var path in directRoleSetPaths.Where(p => p.RoleSetId == rsEntry.Id).OrderBy(p => p.RoleName))
            {
                if (!permissionMap.ContainsKey(path.PermissionId))
                {
                    permissionMap[path.PermissionId] = new PermissionEntryDto(
                        path.PermissionId, "", false,
                        [
                            new ProvenanceNodeDto("roleSet", rsEntry.Id.ToString(), "", ""),
                            new ProvenanceNodeDto("role", path.RoleId.ToString(), "", ""),
                            new ProvenanceNodeDto("permission", path.PermissionId, "", ""),
                        ]);
                }
            }
        }

        // Apply request-body DENY overrides (no DB overrides in preview mode).
        var deniedFromRequest = request.Overrides
            .Where(o => string.Equals(o.Effect, "DENY", StringComparison.OrdinalIgnoreCase))
            .Select(o => o.PermissionId)
            .ToHashSet();

        foreach (var permId in deniedFromRequest)
            permissionMap.Remove(permId);

        return new EffectivePermissionsResponse(
            "",
            DateTimeOffset.UtcNow.ToString("O"),
            request.GroupIds.Count > 0,
            permissionMap.Values.ToList());
    }
}
