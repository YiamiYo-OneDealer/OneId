using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

// Stub configuration — GroupId has no FK to Group entity until Story 4a.4.
public class GroupRoleConfiguration : IEntityTypeConfiguration<GroupRole>
{
    public void Configure(EntityTypeBuilder<GroupRole> builder)
    {
        builder.HasKey(gr => new { gr.GroupId, gr.RoleId });
        builder.Property(gr => gr.GroupId).IsRequired();
    }
}
