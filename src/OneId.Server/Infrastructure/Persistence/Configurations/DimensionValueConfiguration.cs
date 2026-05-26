using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class DimensionValueConfiguration : IEntityTypeConfiguration<DimensionValue>
{
    public void Configure(EntityTypeBuilder<DimensionValue> builder)
    {
        builder.ToTable("dimension_values");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Value).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Axis).HasConversion<int>();
        builder.HasIndex(d => new { d.TenantId, d.Axis, d.Value }).IsUnique();
        // AR-14: manual xmin shadow property (Npgsql v10)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        // DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
    }
}
