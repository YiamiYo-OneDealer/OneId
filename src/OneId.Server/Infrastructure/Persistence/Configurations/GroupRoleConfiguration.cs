using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneId.Server.Domain.Entities;

namespace OneId.Server.Infrastructure.Persistence.Configurations;

public class GroupRoleConfiguration : IEntityTypeConfiguration<GroupRole>
{
    public void Configure(EntityTypeBuilder<GroupRole> builder)
    {
        builder.HasKey(gr => new { gr.GroupId, gr.RoleId });
        builder.HasOne(gr => gr.Group).WithMany(g => g.GroupRoles).HasForeignKey(gr => gr.GroupId).OnDelete(DeleteBehavior.Cascade);
    }
}
