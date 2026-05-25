using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
        builder.Property(u => u.DeletedAt);
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.AccessFailedCount).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.LockoutEnd);
        builder.Property(u => u.TotpSecret).HasMaxLength(500);
        builder.Property(u => u.IsTotpEnrolled).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.TotpLastUsedTimeStep);

        // Unique email per tenant (not globally — same email can exist across tenants)
        builder.HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");

        // AR-14: xmin-based optimistic concurrency for User (UseXminAsConcurrencyToken removed in Npgsql v10)
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // DO NOT add HasQueryFilter here — the global query filter references ITenantContext
        // which is only available in AppDbContext, not in IEntityTypeConfiguration<T>.
        // The tenant-isolation + soft-delete filter is added in AppDbContext.OnModelCreating.
    }
}
