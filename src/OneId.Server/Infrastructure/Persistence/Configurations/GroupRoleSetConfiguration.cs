using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

// Stub configuration — GroupId has no FK to Group entity until Story 4a.4.
public class GroupRoleSetConfiguration : IEntityTypeConfiguration<GroupRoleSet>
{
    public void Configure(EntityTypeBuilder<GroupRoleSet> builder)
    {
        builder.HasKey(grs => new { grs.GroupId, grs.RoleSetId });
        builder.Property(grs => grs.GroupId).IsRequired();
    }
}
