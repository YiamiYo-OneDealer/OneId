using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class UserPermissionOverrideConfiguration : IEntityTypeConfiguration<UserPermissionOverride>
{
    public void Configure(EntityTypeBuilder<UserPermissionOverride> builder)
    {
        builder.ToTable("user_permission_overrides");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.PermissionId).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Reason).IsRequired().HasMaxLength(500);

        builder.HasIndex(o => new { o.TenantId, o.UserId, o.PermissionId }).IsUnique();

        // AR-14: manual xmin shadow property (Npgsql v10)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        // DO NOT add HasQueryFilter here — global filter is set in AppDbContext.OnModelCreating
    }
}
