using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OneId.Server.Domain.Entities;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

internal static class DevSeeder
{
    // Stable well-known IDs — idempotency and TestTokenFactory alignment.
    public static readonly Guid DevTenantId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    // AR-6: Runs only after global query filters are active.
    // Called from Program.cs inside the IsDevelopment/Docker block, after db.Database.MigrateAsync().
    public static async Task SeedAsync(AppDbContext db, IOpenIddictApplicationManager manager)
    {
        await SeedDevTenantAsync(db);
        await SeedAdminUserAsync(db);
        await SeedOpenIddictClientAsync(manager);
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

    private static async Task SeedOpenIddictClientAsync(IOpenIddictApplicationManager manager)
    {
        if (await manager.FindByClientIdAsync("oneid-dev-client") is not null) return;

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
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
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                $"{Permissions.Prefixes.Scope}openid",
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        });
    }
}
