using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Seeds;

internal static class DevSeeder
{
    // Stable well-known IDs — idempotency and TestTokenFactory alignment.
    public static readonly Guid DevTenantId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid AdminUserId  = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    // AR-6: Runs only after global query filters are active.
    // Called from Program.cs inside the IsDevelopment block, after db.Database.MigrateAsync().
    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedDevTenantAsync(db);
        await SeedAdminUserAsync(db);
        // TODO Story 2.1: after AddOpenIddict() is registered, inject IOpenIddictApplicationManager
        // here and call SeedOpenIddictClientAsync(manager).
        // Registration: client_id = "oneid-dev-client", redirect_uri = "http://localhost:3000/callback"
        // Use OpenIddictApplicationDescriptor with ClientType = Public (SPA PKCE flow).
    }

    private static async Task SeedDevTenantAsync(AppDbContext db)
    {
        var exists = await db.Tenants.AnyAsync(t => t.Id == DevTenantId);
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
}
