using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

// Idempotent. Runs on every startup in all environments to ensure the OneId system
// tenant, its platform permissions, and default roles/groups are always present.
public static class SystemSeeder
{
    public static readonly Guid SystemTenantId        = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid OneIdAdminRoleId      = Guid.Parse("00000000-0000-0000-0001-000000000001");
    public static readonly Guid OneIdDeveloperRoleId  = Guid.Parse("00000000-0000-0000-0001-000000000002");
    public static readonly Guid OneIdAdminsGroupId    = Guid.Parse("00000000-0000-0000-0002-000000000001");
    public static readonly Guid OneIdDevelopersGroupId = Guid.Parse("00000000-0000-0000-0002-000000000002");

    public static async Task SeedAsync(AppDbContext db)
    {
        try
        {
            await SeedSystemTenantAsync(db);
            await SeedOneIdPermissionsAsync(db);
            await SeedOneIdAdminRoleAsync(db);
            await SeedOneIdDeveloperRoleAsync(db);
            await SeedOneIdGroupsAsync(db);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Concurrent startup — another instance won the race and already inserted this row.
            // Clear tracked-but-unsaved entities so the DbContext is clean for callers.
            db.ChangeTracker.Clear();
        }
    }

    private static async Task SeedSystemTenantAsync(AppDbContext db)
    {
        var exists = await db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == SystemTenantId);
        if (exists) return;

        db.Tenants.Add(new Tenant
        {
            Id = SystemTenantId,
            Name = "OneId",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedOneIdPermissionsAsync(AppDbContext db)
    {
        foreach (var entry in PermissionCatalog.OneIdEntries)
        {
            var exists = await db.Permissions.AnyAsync(p => p.PermissionId == entry.PermissionId);
            if (exists) continue;

            db.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                PermissionId = entry.PermissionId,
                Label = entry.Label,
                Status = PermissionStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedOneIdAdminRoleAsync(AppDbContext db)
    {
        var allOneIdPermIds = PermissionCatalog.OneIdEntries.Select(e => e.PermissionId);
        await EnsureRoleWithPermissionsAsync(db, OneIdAdminRoleId, "OneId Admin", allOneIdPermIds);
    }

    private static async Task SeedOneIdDeveloperRoleAsync(AppDbContext db)
    {
        string[] developerPermIds =
        [
            Permissions.OneIdPermissionsView,
            Permissions.OneIdPermissionsCreate,
            Permissions.OneIdPermissionsUpdate,
        ];
        await EnsureRoleWithPermissionsAsync(db, OneIdDeveloperRoleId, "OneId Developer", developerPermIds);
    }

    private static async Task EnsureRoleWithPermissionsAsync(
        AppDbContext db, Guid roleId, string name, IEnumerable<string> permissionIds)
    {
        var exists = await db.Roles.IgnoreQueryFilters().AnyAsync(r => r.Id == roleId);
        if (!exists)
        {
            db.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = SystemTenantId,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        foreach (var permId in permissionIds)
        {
            var perm = await db.Permissions.FirstOrDefaultAsync(p => p.PermissionId == permId);
            if (perm is null) continue;

            var alreadyLinked = await db.RolePermissions
                .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == perm.Id);
            if (!alreadyLinked)
                db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedOneIdGroupsAsync(AppDbContext db)
    {
        await EnsureGroupWithRoleAsync(db, OneIdAdminsGroupId,    "OneId Admins",    OneIdAdminRoleId);
        await EnsureGroupWithRoleAsync(db, OneIdDevelopersGroupId, "OneId Developers", OneIdDeveloperRoleId);
    }

    private static async Task EnsureGroupWithRoleAsync(
        AppDbContext db, Guid groupId, string name, Guid roleId)
    {
        var exists = await db.Groups.IgnoreQueryFilters().AnyAsync(g => g.Id == groupId);
        if (!exists)
        {
            db.Groups.Add(new Group
            {
                Id = groupId,
                TenantId = SystemTenantId,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var linked = await db.GroupRoles.AnyAsync(gr => gr.GroupId == groupId && gr.RoleId == roleId);
        if (!linked)
        {
            db.GroupRoles.Add(new GroupRole { GroupId = groupId, RoleId = roleId });
            await db.SaveChangesAsync();
        }
    }
}
