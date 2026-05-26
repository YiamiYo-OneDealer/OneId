using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class RoleSetConfiguration : IEntityTypeConfiguration<RoleSet>
{
    public void Configure(EntityTypeBuilder<RoleSet> builder)
    {
        builder.HasKey(rs => rs.Id);
        builder.Property(rs => rs.TenantId).IsRequired();
        builder.Property(rs => rs.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(rs => new { rs.TenantId, rs.Name }).IsUnique();
        builder.Property(rs => rs.CreatedAt).IsRequired();
        builder.Property(rs => rs.UpdatedAt).IsRequired();
        builder.HasMany(rs => rs.RoleSetRoles)
            .WithOne(rsr => rsr.RoleSet)
            .HasForeignKey(rsr => rsr.RoleSetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(rs => rs.GroupRoleSets)
            .WithOne(grs => grs.RoleSet)
            .HasForeignKey(grs => grs.RoleSetId)
            .OnDelete(DeleteBehavior.Restrict);
        // AR-14: manual xmin shadow property (Npgsql v10)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        // DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
    }
}
