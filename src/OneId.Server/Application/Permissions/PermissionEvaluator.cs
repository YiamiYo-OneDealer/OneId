using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Enums;
using OneId.Server.Domain.Services;
using OneId.Server.Infrastructure.Persistence;

namespace OneId.Server.Application.Permissions;

// Called during token issuance where ITenantContext is not yet initialized.
// Uses explicit tenantId parameter for isolation instead of EF query filters.
public sealed class PermissionEvaluator(AppDbContext db, ICacheService cache) : IPermissionEvaluator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlySet<string>> EvaluateAsync(Guid userId, Guid tenantId, CancellationToken ct = default)
    {
        var cacheKey = $"permissions:{userId}:{tenantId}";
        var cached = cache.Get<HashSet<string>>(cacheKey);
        if (cached is not null)
            return cached;

        // Collect role IDs from direct Group → GroupRole chain.
        // UserGroups and GroupRoles have no tenant query filter — safe to query directly.
        var directRoleIds = await db.UserGroups
            .Where(ug => ug.UserId == userId)
            .Join(db.GroupRoles, ug => ug.GroupId, gr => gr.GroupId, (_, gr) => gr.RoleId)
            .Distinct()
            .ToListAsync(ct);

        // Collect role IDs via Group → GroupRoleSet → RoleSetRole chain.
        var roleSetRoleIds = await db.UserGroups
            .Where(ug => ug.UserId == userId)
            .Join(db.GroupRoleSets, ug => ug.GroupId, grs => grs.GroupId, (_, grs) => grs.RoleSetId)
            .Join(db.RoleSetRoles, grsId => grsId, rsr => rsr.RoleSetId, (_, rsr) => rsr.RoleId)
            .Distinct()
            .ToListAsync(ct);

        // Merge all role IDs and resolve to permission string IDs.
        // RolePermissions and Permissions have no tenant query filter.
        var allRoleIds = directRoleIds.Union(roleSetRoleIds).ToList();

        var groupPermissions = allRoleIds.Count > 0
            ? await db.RolePermissions
                .Where(rp => allRoleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.PermissionId)
                .Distinct()
                .ToListAsync(ct)
            : [];

        // Active overrides only: bypass tenant query filter using explicit tenantId filter.
        var now = DateTimeOffset.UtcNow;
        var overrides = await db.UserPermissionOverrides
            .IgnoreQueryFilters()
            .Where(o => o.UserId == userId && o.TenantId == tenantId
                        && (o.ExpiresAt == null || o.ExpiresAt > now))
            .Select(o => new { o.PermissionId, o.OverrideType })
            .ToListAsync(ct);

        // Build union from group-sourced permissions.
        var permissions = new HashSet<string>(groupPermissions);

        // DENY is terminal — collect all denied permission IDs first.
        var deniedPermissions = overrides
            .Where(o => o.OverrideType == PermissionOverrideType.Deny)
            .Select(o => o.PermissionId)
            .ToHashSet();

        permissions.ExceptWith(deniedPermissions);

        // Additive ALLOW overrides — skipped if also in the deny set (defensive, unique constraint prevents this).
        foreach (var allow in overrides.Where(o => o.OverrideType == PermissionOverrideType.Allow))
        {
            if (!deniedPermissions.Contains(allow.PermissionId))
                permissions.Add(allow.PermissionId);
        }

        cache.Set(cacheKey, permissions, CacheTtl);
        return permissions;
    }
}
