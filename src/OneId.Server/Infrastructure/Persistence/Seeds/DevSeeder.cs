using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OdPermissions = OneId.Server.Application.Common.Permissions;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

public static class DevSeeder
{
    // Stable well-known IDs — idempotency and TestTokenFactory alignment.
    public static readonly Guid DevTenantId      = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId      = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public static readonly Guid TotpUserId       = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    public static readonly Guid TenantUserId     = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
    public static readonly Guid DemoGroupId      = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    public static readonly Guid DemoRoleId       = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    public static readonly string TotpUserEmail  = "totp@oneid.dev";
    public static readonly string TenantUserEmail = "user@dev-tenant.oneid.dev";
    // Standard OtpNet test vector — stable base32 secret for pre-enrolled integration test user.
    public const string TotpUserTotpSecret = "JBSWY3DPEHPK3PXP";

    // AR-6: Runs only after global query filters are active.
    // Called from Program.cs inside the IsDevelopment/Docker block, after db.Database.MigrateAsync().
    public static async Task SeedAsync(AppDbContext db, IOpenIddictApplicationManager manager, IDataProtectionProvider dp)
    {
        await SeedDevTenantAsync(db);
        await SeedAdminUserAsync(db);
        await SeedTotpUserAsync(db, dp);
        await SeedOpenIddictClientAsync(manager);
        await SeedSampleAppClientAsync(manager);
        await SeedPermissionsAsync(db);
        await SeedTenantUserWithPermissionsAsync(db);
    }

    internal static async Task SeedPermissionsAsync(AppDbContext db)
    {
        foreach (var entry in PermissionCatalog.SeedEntries)
        {
            var exists = await db.Permissions
                .AnyAsync(p => p.PermissionId == entry.PermissionId);
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

    private static async Task SeedDevTenantAsync(AppDbContext db)
    {
        // IgnoreQueryFilters: Tenant has a soft-delete filter. If the dev tenant was previously
        // soft-deleted, AnyAsync without this would return false and re-insert would hit the
        // unique-name constraint, aborting startup.
        var exists = await db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == DevTenantId);
        if (exists) return;

        db.Tenants.Add(new Tenant
        {
            Id = DevTenantId,
            Name = "Dev Tenant",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedAdminUserAsync(AppDbContext db)
    {
        // IgnoreQueryFilters: User has a TenantId global filter. DevSeeder does not activate
        // ITenantContext — IgnoreQueryFilters avoids the Guid.Empty filter trapping this lookup.
        var exists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Id == AdminUserId);
        if (exists) return;

        var user = new User
        {
            Id = AdminUserId,
            TenantId = DevTenantId,
            Email = "admin@oneid.dev",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Admin123!");

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static async Task SeedTotpUserAsync(AppDbContext db, IDataProtectionProvider dp)
    {
        var exists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Id == TotpUserId);
        if (!exists)
        {
            var user = new User
            {
                Id = TotpUserId,
                TenantId = SystemSeeder.SystemTenantId,
                Email = TotpUserEmail,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                IsTotpEnrolled = true,
                TotpSecret = dp.CreateProtector("totp.secret.v1").Protect(TotpUserTotpSecret),
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Admin123!");

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        // Add to OneId Admins group (idempotent).
        var ugExists = await db.UserGroups.IgnoreQueryFilters()
            .AnyAsync(ug => ug.UserId == TotpUserId && ug.GroupId == SystemSeeder.OneIdAdminsGroupId);
        if (!ugExists)
        {
            db.UserGroups.Add(new UserGroup { UserId = TotpUserId, GroupId = SystemSeeder.OneIdAdminsGroupId });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedOpenIddictClientAsync(IOpenIddictApplicationManager manager)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "oneid-dev-client",
            ClientType = ClientTypes.Public,
            DisplayName = "OneId Dev SPA Client",
            RedirectUris = { new Uri("http://localhost:3000/callback") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                $"{Permissions.Prefixes.GrantType}urn:oneid:mfa",
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                $"{Permissions.Prefixes.Scope}offline_access",
                $"{Permissions.Prefixes.Scope}openid",
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        var existing = await manager.FindByClientIdAsync("oneid-dev-client");
        if (existing is null)
            await manager.CreateAsync(descriptor);
        else
            await manager.UpdateAsync(existing, descriptor);
    }

    // Seeds a plain tenant user wired to a group → role → permissions so the sample client
    // demo shows a non-empty permissions array in both the access token and introspection response.
    private static async Task SeedTenantUserWithPermissionsAsync(AppDbContext db)
    {
        var userExists = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Id == TenantUserId);
        if (!userExists)
        {
            var user = new User
            {
                Id = TenantUserId,
                TenantId = DevTenantId,
                Email = TenantUserEmail,
                DisplayName = "Demo Tenant User",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "User123!");
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var groupExists = await db.Groups.IgnoreQueryFilters()
            .AnyAsync(g => g.Id == DemoGroupId);
        if (!groupExists)
        {
            db.Groups.Add(new Group
            {
                Id = DemoGroupId,
                TenantId = DevTenantId,
                Name = "Demo Operators",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var roleExists = await db.Roles.IgnoreQueryFilters()
            .AnyAsync(r => r.Id == DemoRoleId);
        if (!roleExists)
        {
            db.Roles.Add(new Role
            {
                Id = DemoRoleId,
                TenantId = DevTenantId,
                Name = "Demo Operator Role",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Wire role → permissions (idempotent via Permission FK lookup).
        var permissionIdsToGrant = new[]
        {
            OdPermissions.CrmRead,
            OdPermissions.CrmWrite,
            OdPermissions.FinanceRead,
            OdPermissions.AdminUsersView,
            OdPermissions.AdminRolesView,
            OdPermissions.AdminGroupsView,
            OdPermissions.AdminAuditView,
        };

        foreach (var permId in permissionIdsToGrant)
        {
            var permEntity = await db.Permissions.FirstOrDefaultAsync(p => p.PermissionId == permId);
            if (permEntity is null) continue;

            var rpExists = await db.RolePermissions
                .AnyAsync(rp => rp.RoleId == DemoRoleId && rp.PermissionId == permEntity.Id);
            if (!rpExists)
                db.RolePermissions.Add(new RolePermission { RoleId = DemoRoleId, PermissionId = permEntity.Id });
        }
        await db.SaveChangesAsync();

        var grExists = await db.GroupRoles.AnyAsync(gr => gr.GroupId == DemoGroupId && gr.RoleId == DemoRoleId);
        if (!grExists)
            db.GroupRoles.Add(new GroupRole { GroupId = DemoGroupId, RoleId = DemoRoleId });

        var ugExists = await db.UserGroups.AnyAsync(ug => ug.UserId == TenantUserId && ug.GroupId == DemoGroupId);
        if (!ugExists)
            db.UserGroups.Add(new UserGroup { UserId = TenantUserId, GroupId = DemoGroupId });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Confidential client used by the sample app to call the introspection endpoint.
    /// Acts as a resource server — no user-facing grants, only introspection permission.
    /// </summary>
    private static async Task SeedSampleAppClientAsync(IOpenIddictApplicationManager manager)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "oneid-sample-app",
            ClientSecret = "sample-app-secret",
            ClientType = ClientTypes.Confidential,
            DisplayName = "OneId Sample App (resource server)",
            Permissions =
            {
                Permissions.Endpoints.Introspection,
            },
        };

        var existing = await manager.FindByClientIdAsync("oneid-sample-app");
        if (existing is null)
            await manager.CreateAsync(descriptor);
        else
            await manager.UpdateAsync(existing, descriptor);
    }
}
