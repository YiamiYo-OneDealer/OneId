using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(r => new { r.TenantId, r.Name }).IsUnique();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.GroupRoles)
            .WithOne(gr => gr.Role)
            .HasForeignKey(gr => gr.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // AR-14: xmin-based optimistic concurrency (UseXminAsConcurrencyToken removed in Npgsql v10)
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // DO NOT add HasQueryFilter here — global filter is in AppDbContext.OnModelCreating
    }
}
