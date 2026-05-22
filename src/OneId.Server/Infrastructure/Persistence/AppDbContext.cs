using Microsoft.EntityFrameworkCore;

namespace OneId.Server.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // snake_case naming is applied via UseSnakeCaseNamingConvention() on DbContextOptionsBuilder in Program.cs
        // (EFCore.NamingConventions v6+ API — not a ModelBuilder extension)

        // AR-14: UseXminAsConcurrencyToken applied to all mutable entities.
        // Each epic that introduces a new mutable entity is responsible for adding it here.
        // Story 1.3b adds: Tenant, User
        // Epic 3 adds: License, IdpConfiguration, AuditLog
        // Epic 4a adds: Role, RoleSet, Group, Permission, DimensionValue, UserDimensionAssignment
    }
}
