using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

public static class DevSeeder
{
    // Stable well-known IDs — idempotency and TestTokenFactory alignment.
    public static readonly Guid DevTenantId  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    public static readonly Guid TotpUserId   = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    public static readonly string TotpUserEmail = "totp@oneid.dev";
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
        if (exists) return;

        var user = new User
        {
            Id = TotpUserId,
            TenantId = DevTenantId,
            Email = TotpUserEmail,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsTotpEnrolled = true,
            IsTenantAdmin = true,
            IsInternalAdmin = true,
            TotpSecret = dp.CreateProtector("totp.secret.v1").Protect(TotpUserTotpSecret),
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "Admin123!");

        db.Users.Add(user);
        await db.SaveChangesAsync();
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
