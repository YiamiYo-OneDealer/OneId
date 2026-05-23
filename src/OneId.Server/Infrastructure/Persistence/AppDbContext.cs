using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext,
    ILogger<AppDbContext> logger)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<User> Users
    {
        get
        {
            if (!tenantContext.IsInitialized)
                logger.LogWarning(
                    "[AR-5] AppDbContext.Users accessed with uninitialized ITenantContext — " +
                    "global filter falls back to Guid.Empty (returns 0 rows). " +
                    "Call .IgnoreQueryFilters() for intentional cross-tenant access.");
            return Set<User>();
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // snake_case naming is applied via UseSnakeCaseNamingConvention() on DbContextOptionsBuilder in Program.cs
        // (EFCore.NamingConventions v6+ API — not a ModelBuilder extension)

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // AR-5 STEP 2: Tenant-isolation global query filter for User
        // Uses IsInitialized guard — prevents guard exception during startup/DevSeeder/IgnoreQueryFilters paths.
        // InternalAdminContext and DevSeeder must call .IgnoreQueryFilters() for cross-tenant access.
        builder.Entity<User>().HasQueryFilter(u =>
            !u.DeletedAt.HasValue &&
            u.TenantId == (tenantContext.IsInitialized ? tenantContext.TenantId : Guid.Empty));

        // AR-14: UseXminAsConcurrencyToken applied to all mutable entities.
        // Each epic that introduces a new mutable entity is responsible for adding it here.
        // Story 1.3b adds: Tenant, User
        // Epic 3 adds: License, IdpConfiguration, AuditLog
        // Epic 4a adds: Role, RoleSet, Group, Permission, DimensionValue, UserDimensionAssignment
        // Note: xmin is a PostgreSQL system column. No migration column is needed. In-memory provider ignores it.
    }
}
