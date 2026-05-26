using Microsoft.EntityFrameworkCore;
using OneId.Server.Application.Common;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Permission> Permissions => Set<Permission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // snake_case naming is applied via UseSnakeCaseNamingConvention() on DbContextOptionsBuilder in Program.cs
        // (EFCore.NamingConventions v6+ API — not a ModelBuilder extension)

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // AR-5 STEP 2: Tenant-isolation global query filter for User.
        // Calls tenantContext.TenantId directly — throws InvalidOperationException if context is
        // uninitialized. Cross-tenant access (DevSeeder, InternalAdmin) must use .IgnoreQueryFilters().
        builder.Entity<User>().HasQueryFilter(u =>
            !u.DeletedAt.HasValue &&
            u.TenantId == tenantContext.TenantId);

        // Tenant-isolation filter for AuditLog reads.
        // InternalAdmin handlers use .IgnoreQueryFilters() for cross-tenant reads.
        builder.Entity<AuditLog>().HasQueryFilter(a =>
            a.TenantId == tenantContext.TenantId);

        // AR-14: UseXminAsConcurrencyToken applied to all mutable entities.
        // Each epic that introduces a new mutable entity is responsible for adding it here.
        // Story 1.3b adds: Tenant, User
        // Epic 3 adds: License, IdpConfiguration, AuditLog
        // Story 4a.1 adds: Permission (via PermissionConfiguration)
        // Epic 4a adds: Role, RoleSet, Group, DimensionValue, UserDimensionAssignment
        // Note: xmin is a PostgreSQL system column. No migration column is needed. In-memory provider ignores it.
    }
}
