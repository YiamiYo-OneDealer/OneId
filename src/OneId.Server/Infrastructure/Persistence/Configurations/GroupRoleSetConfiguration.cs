using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class GroupRoleSetConfiguration : IEntityTypeConfiguration<GroupRoleSet>
{
    public void Configure(EntityTypeBuilder<GroupRoleSet> builder)
    {
        builder.HasKey(grs => new { grs.GroupId, grs.RoleSetId });
        builder.HasOne(grs => grs.Group).WithMany(g => g.GroupRoleSets).HasForeignKey(grs => grs.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
