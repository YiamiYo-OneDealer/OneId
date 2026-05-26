using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;
using OneId.Server.Domain.Enums;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasDefaultValue(TenantStatus.Active)
            .HasConversion<int>();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
        builder.Property(t => t.DeletedAt);

        builder.HasIndex(t => t.Name).IsUnique();

        // AR-14: xmin-based optimistic concurrency for Tenant (UseXminAsConcurrencyToken removed in Npgsql v10)
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Soft-delete filter — Tenant is NOT tenant-scoped, so no TenantId filter
        builder.HasQueryFilter(t => !t.DeletedAt.HasValue);
    }
}
