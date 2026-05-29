using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Permissions;

public sealed class GetEffectivePermissionsHandler(AppDbContext db, ITenantContext tenantContext)
{
    public async Task<EffectivePermissionsResponse?> HandleAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId;

        // Tenant isolation: target user must belong to the calling admin's tenant.
        var userInTenant = await db.Users
            .AnyAsync(u => u.Id == targetUserId, ct);
        if (!userInTenant) return null;

        var now = DateTimeOffset.UtcNow;

        // Load user's groups in the tenant (query filter ensures tenant isolation).
        // Ordered by group name for deterministic first-path selection.
        var userGroups = await (
            from ug in db.UserGroups
            where ug.UserId == targetUserId
            join g in db.Groups on ug.GroupId equals g.Id
            orderby g.Name
            select new { GroupId = g.Id, GroupName = g.Name }
        ).ToListAsync(ct);

        var groupIds = userGroups.Select(g => g.GroupId).ToList();

        // Direct paths: Group → Role → Permission
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

        // RoleSet paths: Group → RoleSet → Role → Permission
        var roleSetPaths = groupIds.Count > 0
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

        // All overrides: bypass tenant query filter, apply explicit tenantId.
        var allOverrides = await db.UserPermissionOverrides
            .IgnoreQueryFilters()
            .Where(o => o.UserId == targetUserId && o.TenantId == tenantId
                        && (o.ExpiresAt == null || o.ExpiresAt > now))
            .Select(o => new { o.PermissionId, o.OverrideType })
            .ToListAsync(ct);

        var deniedSet = allOverrides
            .Where(o => o.OverrideType == PermissionOverrideType.Deny)
            .Select(o => o.PermissionId)
            .ToHashSet();

        var allowSet = allOverrides
            .Where(o => o.OverrideType == PermissionOverrideType.Allow)
            .Select(o => o.PermissionId)
            .ToHashSet();

        var permissionMap = new Dictionary<string, PermissionEntryDto>();

        // Build provenance map: first path wins (groups ordered by name, then roles by name).
        foreach (var group in userGroups)
        {
            foreach (var path in directPaths.Where(p => p.GroupId == group.GroupId).OrderBy(p => p.Name))
            {
                if (!permissionMap.ContainsKey(path.PermissionId))
                {
                    permissionMap[path.PermissionId] = new PermissionEntryDto(
                        path.PermissionId, "", false,
                        [
                            new ProvenanceNodeDto("user", targetUserId.ToString(), "", ""),
                            new ProvenanceNodeDto("group", group.GroupId.ToString(), "", ""),
                            new ProvenanceNodeDto("role", path.RoleId.ToString(), "", ""),
                            new ProvenanceNodeDto("permission", path.PermissionId, "", ""),
                        ]);
                }
            }

            foreach (var path in roleSetPaths.Where(p => p.GroupId == group.GroupId)
                                              .OrderBy(p => p.RoleSetName).ThenBy(p => p.RoleName))
            {
                if (!permissionMap.ContainsKey(path.PermissionId))
                {
                    permissionMap[path.PermissionId] = new PermissionEntryDto(
                        path.PermissionId, "", false,
                        [
                            new ProvenanceNodeDto("user", targetUserId.ToString(), "", ""),
                            new ProvenanceNodeDto("group", group.GroupId.ToString(), "", ""),
                            new ProvenanceNodeDto("roleSet", path.RoleSetId.ToString(), "", ""),
                            new ProvenanceNodeDto("role", path.RoleId.ToString(), "", ""),
                            new ProvenanceNodeDto("permission", path.PermissionId, "", ""),
                        ]);
                }
            }
        }

        // Apply ALLOW overrides: permissions granted explicitly, not via any group, and not denied.
        foreach (var permId in allowSet)
        {
            if (!permissionMap.ContainsKey(permId) && !deniedSet.Contains(permId))
            {
                permissionMap[permId] = new PermissionEntryDto(
                    permId, "", false,
                    [new ProvenanceNodeDto("user", targetUserId.ToString(), "", "")]);
            }
        }

        // Apply DENY overrides: add or mark existing entries.
        foreach (var permId in deniedSet)
        {
            if (permissionMap.TryGetValue(permId, out var existing))
            {
                // Permission was also group-granted or allow-granted: keep chain, mark denied.
                permissionMap[permId] = existing with { IsDenied = true };
            }
            else
            {
                // Pure deny override — single-node user chain.
                permissionMap[permId] = new PermissionEntryDto(
                    permId, "", true,
                    [new ProvenanceNodeDto("user", targetUserId.ToString(), "", "")]);
            }
        }

        return new EffectivePermissionsResponse(
            targetUserId.ToString(),
            now.ToString("O"),
            userGroups.Count > 0,
            permissionMap.Values.ToList());
    }
}
