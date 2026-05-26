using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.TenantId).IsRequired();
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(g => new { g.TenantId, g.Name }).IsUnique();
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt).IsRequired();
        // AR-14: manual xmin shadow property (Npgsql v10)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        // DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
    }
}
