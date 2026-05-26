using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Payload).HasColumnType("jsonb");
        builder.Property(a => a.Timestamp).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.Timestamp });
    }
}
