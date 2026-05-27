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
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<GroupRole> GroupRoles => Set<GroupRole>();
    public DbSet<RoleSet> RoleSets => Set<RoleSet>();
    public DbSet<RoleSetRole> RoleSetRoles => Set<RoleSetRole>();
    public DbSet<GroupRoleSet> GroupRoleSets => Set<GroupRoleSet>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<DimensionValue> DimensionValues => Set<DimensionValue>();
    public DbSet<UserDimensionAssignment> UserDimensionAssignments => Set<UserDimensionAssignment>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

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

        // Story 4a.2: Role tenant isolation.
        builder.Entity<Role>().HasQueryFilter(r => r.TenantId == tenantContext.TenantId);

        // Story 4a.3: RoleSet tenant isolation.
        builder.Entity<RoleSet>().HasQueryFilter(rs => rs.TenantId == tenantContext.TenantId);

        // Story 4a.4: Group tenant isolation.
        builder.Entity<Group>().HasQueryFilter(g => g.TenantId == tenantContext.TenantId);

        // Story 4a.5: DimensionValue tenant isolation.
        builder.Entity<DimensionValue>().HasQueryFilter(d => d.TenantId == tenantContext.TenantId);

        // Story 4a.6: UserDimensionAssignment tenant isolation (via DimensionValue navigation).
        builder.Entity<UserDimensionAssignment>().HasQueryFilter(a =>
            a.DimensionValue.TenantId == tenantContext.TenantId);

        // Story 4b.1: UserPermissionOverride tenant isolation.
        builder.Entity<UserPermissionOverride>().HasQueryFilter(o =>
            o.TenantId == tenantContext.TenantId);

        // AR-14: UseXminAsConcurrencyToken applied to all mutable entities.
        // Each epic that introduces a new mutable entity is responsible for adding it here.
        // Story 1.3b adds: Tenant, User
        // Epic 3 adds: License, IdpConfiguration, AuditLog
        // Story 4a.1 adds: Permission (via PermissionConfiguration)
        // Epic 4a adds: Role, RoleSet, Group, DimensionValue, UserDimensionAssignment
        // Story 4b.1 adds: UserPermissionOverride
        // Note: xmin is a PostgreSQL system column. No migration column is needed. In-memory provider ignores it.
    }
}
